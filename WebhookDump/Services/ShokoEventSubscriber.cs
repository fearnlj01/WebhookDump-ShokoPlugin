using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Services;

public class ShokoEventSubscriber
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private readonly PersistentFileIdDict _cachedFiles;
  private readonly PersistentMessageDict _cachedMessages;
  private readonly Dictionary<int, CancellationTokenSource> _cancellationTokens = [];
  private readonly IMetadataService _metadataService;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IShokoEventHandler _shokoEventHandler;
  private readonly IOptionsMonitor<ShokoSettings> _shokoOptionsMonitor;

  private readonly IVideoService _videoService;
  private readonly IOptionsMonitor<WebhookSettings> _webhookOptionsMonitor;


  public ShokoEventSubscriber(
    IVideoService videoService,
    IMetadataService metadataService,
    IShokoEventHandler shokoEventHandler,
    IOptionsMonitor<ShokoSettings> shokoOptionsMonitor,
    IOptionsMonitor<WebhookSettings> webhookOptionsMonitor,
    IServiceScopeFactory scopeFactory, PersistentFileIdDict cachedFiles, PersistentMessageDict cachedMessages)
  {
    _cachedFiles = cachedFiles;
    _cachedMessages = cachedMessages;
    _videoService = videoService;
    _scopeFactory = scopeFactory;
    _metadataService = metadataService;
    _shokoEventHandler = shokoEventHandler;
    _shokoOptionsMonitor = shokoOptionsMonitor;
    _webhookOptionsMonitor = webhookOptionsMonitor;

    _videoService.VideoFileMatched += (sender, args) => { _ = OnVideoFileMatched(args); };
    _videoService.VideoFileNotMatched += (sender, args) => { _ = OnFileNotMatched(args); };
    _videoService.VideoFileDeleted += (_, args) => { OnVideoFileDeletedEvent(args); };

    _shokoEventHandler.AVDumpEvent += (sender, args) => { _ = OnAVDumpEvent(args); };

    _metadataService.EpisodeUpdated += (sender, args) => { _ = OnEpisodeUpdatedEvent(args); };
  }

  private WebhookSettings WebhookSettings => _webhookOptionsMonitor.CurrentValue;
  private ShokoSettings ShokoSettings => _shokoOptionsMonitor.CurrentValue;

  private async Task DelayRescan(int videoId, int matchAttempts, CancellationToken ct)
  {
    if (!_cachedFiles.Contains(videoId)) return;
    if (!ShokoSettings.AutomaticMatch.Enabled || matchAttempts > ShokoSettings.AutomaticMatch.MaxAttempts) return;

    var waitTime = TimeSpan.FromMinutes(5) * Math.Pow(2, matchAttempts - 1) - TimeSpan.FromSeconds(30);
    await Task.Delay(waitTime, ct).ConfigureAwait(false);
  }

  #region Events

  private async Task OnAVDumpEvent(AVDumpEventArgs dumpEvent)
  {
    if (dumpEvent.Type is not AVDumpEventType.Success || dumpEvent.VideoIDs is not { Count: > 0 } ||
        !WebhookSettings.Enabled) return;

    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();
    await discord.SendUnmatchedWebhooks(dumpEvent.VideoIDs).ConfigureAwait(false);
  }

  private async Task OnFileNotMatched(FileNotMatchedEventArgs notMatchedEvent)
  {
    var video = notMatchedEvent.Video;

    if (notMatchedEvent.HasCrossReferences || notMatchedEvent.Video.MediaInfo is null) return;

    using (var dumpScope = _scopeFactory.CreateScope())
    {
      if (notMatchedEvent.AutoMatchAttempts == 1)
        await dumpScope.ServiceProvider.GetRequiredService<ShokoService>().DumpFile(video)
          .ConfigureAwait(false);
    }

    using var cts = AddOrUpdateToken(video.ID);
    try
    {
      await DelayRescan(video.ID, notMatchedEvent.AutoMatchAttempts, cts.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      return;
    }

    using var rescanScope = _scopeFactory.CreateScope();
    await rescanScope.ServiceProvider.GetRequiredService<ShokoService>()
      .RescanFile(video, notMatchedEvent.AutoMatchAttempts).ConfigureAwait(false);
  }

  private async Task OnVideoFileMatched(FileEventArgs matchedEvent)
  {
    var video = matchedEvent.Video;
    var episodes = matchedEvent.Episodes;
    var series = matchedEvent.Series;

    if (!_cachedFiles.Contains(video.ID)) return;

    CancelToken(video.ID); // Stop any pre-existing rescan tasks.

    if (episodes.Count == 0) return; // We'll pick this up with an EpisodeUpdated event
    if (series.Count == 0)
    {
      Logger.Debug(
        "@fearnlj01 is not sure how this can occur... Let him know on discord that it can (log files appreciated)");
      return;
    }

    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();

    await discord.PatchMatchedWebhooks([video], episodes[0], series[0]).ConfigureAwait(false);
  }

  private async Task OnEpisodeUpdatedEvent(EpisodeInfoUpdatedEventArgs episodeUpdatedEvent)
  {
    if (episodeUpdatedEvent.Reason is not (UpdateReason.Added or UpdateReason.Updated)) return;

    var episode = episodeUpdatedEvent.EpisodeInfo;
    var series = episodeUpdatedEvent.SeriesInfo;

    var trackedVideos = episode.VideoList
      .Where(v => _cachedFiles.Contains(v.ID))
      .ToHashSet();

    if (trackedVideos.Count == 0) return;

    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();

    await discord.PatchMatchedWebhooks(trackedVideos, episode, series).ConfigureAwait(false);
  }

  private void OnVideoFileDeletedEvent(FileEventArgs fileDeletedEvent)
  {
    _cachedFiles.Remove(fileDeletedEvent.Video.ID);
    _cachedMessages.Remove(fileDeletedEvent.Video.ID);
  }

  #endregion Events

  #region CancellationToken Management

  private CancellationTokenWrapper<int> AddOrUpdateToken(int videoId)
  {
    if (_cancellationTokens.TryGetValue(videoId, out var existingCts))
    {
      existingCts.Cancel();
      existingCts.Dispose();
    }

    var cts = new CancellationTokenSource();
    _cancellationTokens[videoId] = cts;
    return new CancellationTokenWrapper<int>(videoId, _cancellationTokens, cts);
  }

  private void CancelToken(int videoId)
  {
    if (!_cancellationTokens.TryGetValue(videoId, out var cts)) return;
    cts.Cancel();
    cts.Dispose();
    _cancellationTokens.Remove(videoId);
  }

  #endregion CancellationToken Management
}
