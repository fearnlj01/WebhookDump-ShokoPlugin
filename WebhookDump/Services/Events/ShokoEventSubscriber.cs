using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Video;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Events;

public partial class ShokoEventSubscriber : IDisposable, IInitializable
{
  private readonly IAnidbService _anidbService;
  private readonly ICachedData _cachedData;

  private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTokens = [];
  private readonly Func<AutomaticMatchConfiguration> _getAutoMatchConfiguration;
  private readonly Func<WebhookConfiguration> _getWebhookConfiguration;
  private readonly ILogger<ShokoEventSubscriber> _logger;
  private readonly IMetadataService _metadataService;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ShokoService _shokoService;
  private readonly IVideoReleaseService _videoReleaseService;
  private readonly IVideoService _videoService;

  public ShokoEventSubscriber(
    IVideoService videoService,
    IVideoReleaseService videoReleaseService,
    IMetadataService metadataService,
    IAnidbService anidbService,
    ICachedData cachedData,
    ShokoService shokoService,
    ILogger<ShokoEventSubscriber> logger,
    Func<AutomaticMatchConfiguration> getAutoMatchConfiguration,
    Func<WebhookConfiguration> getWebhookConfiguration,
    IServiceScopeFactory scopeFactory
  )
  {
    _cachedData = cachedData;
    _shokoService = shokoService;
    _logger = logger;
    _videoService = videoService;
    _videoReleaseService = videoReleaseService;
    _scopeFactory = scopeFactory;
    _metadataService = metadataService;
    _anidbService = anidbService;

    _getWebhookConfiguration = getWebhookConfiguration;
    _getAutoMatchConfiguration = getAutoMatchConfiguration;

    _videoReleaseService.SearchCompleted += OnSearchCompleted;
    _videoReleaseService.ReleaseSaved += OnReleaseSaved;
    _videoService.VideoFileDeleted += OnVideoFileDeleted;
    _anidbService.AvdumpEvent += OnAvdumpEvent;
    _metadataService.EpisodeAdded += OnEpisodeAdded;
  }

  private WebhookConfiguration WebhookConfiguration => _getWebhookConfiguration();
  private AutomaticMatchConfiguration AutoMatchConfiguration => _getAutoMatchConfiguration();

  public void Dispose()
  {
    _videoReleaseService.SearchCompleted -= OnSearchCompleted;
    _videoReleaseService.ReleaseSaved -= OnReleaseSaved;
    _videoService.VideoFileDeleted -= OnVideoFileDeleted;
    _anidbService.AvdumpEvent -= OnAvdumpEvent;
    _metadataService.EpisodeUpdated -= OnEpisodeAdded;

    var sources = _cancellationTokens.Values.ToArray();
    _cancellationTokens.Clear();
    foreach (var cts in sources)
    {
      try
      {
        cts.Cancel();
      }
      catch
      {
        // Best effort to cancel, if we can't do so, it's not a problem
      }

      cts.Dispose();
    }

    GC.SuppressFinalize(this);
  }

  public Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }

  [LoggerMessage(LogLevel.Debug,
    "Neither series nor episode is defined for a matched video. (VideoId={video}, Episodes={episodes}, Series={series})")]
  partial void LogNeitherSeriesNorEpisodeIsDefinedForAMatchedVideo(IVideo video, IReadOnlyList<IShokoEpisode> episodes,
    IReadOnlyList<IShokoSeries> series);

  #region OnEvent

  private void OnSearchCompleted(object? sender, VideoReleaseSearchCompletedEventArgs args)
  {
    if (args.IsSuccessful || args.AttemptedProviders.All(rp => rp.Name != "AniDB")) return;
    if (args.Video.CrossReferences.Any(x => x.AnidbAnime is not null)) return;
    if (args.Video.MediaInfo is null) return;

    _ = HandleSearchCompleted(args);
  }

  private void OnReleaseSaved(object? sender, VideoReleaseSavedEventArgs args)
  {
    _ = HandleReleaseSaved(args);
  }

  private void OnAvdumpEvent(object? sender, AvdumpEventArgs args)
  {
    if (args.Type is not AVDumpEventType.Success || args.VideoIDs is not { Count: > 0 } ||
        !WebhookConfiguration.Enabled) return;
    _ = HandleAvDumpEvent(args);
  }

  private void OnEpisodeAdded(object? sender, EpisodeInfoUpdatedEventArgs args)
  {
    _ = HandleEpisodeAdded(args);
  }

  private void OnVideoFileDeleted(object? sender, FileEventArgs args)
  {
    _ = HandleVideoFileDeleted(args);
  }

  #endregion

  #region HandleEvent

  private async Task HandleSearchCompleted(VideoReleaseSearchCompletedEventArgs args)
  {
    var attempts = _videoReleaseService
      .GetReleaseMatchAttemptsForVideo(args.Video)
      .Count(ma => ma.AttemptedProviderNames.Contains("AniDB"));

    if (attempts == 1)
    {
      await _cachedData.SaveTrackedFilesAsync(args.Video.ID).ConfigureAwait(false);
      await _shokoService.DumpFile(args.Video).ConfigureAwait(false);
    }

    if (
      !AutoMatchConfiguration.Enabled ||
      attempts > AutoMatchConfiguration.MaxAttempts
    ) return;

    var cts = _cancellationTokens.GetOrAdd(args.Video.ID, _ => new CancellationTokenSource());
    try
    {
      var waitTime = TimeSpan.FromMinutes(5) * Math.Pow(2, attempts - 1) - TimeSpan.FromSeconds(30);
      await Task.Delay(waitTime, cts.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
    {
      return;
    }

    await _shokoService.RescanFile(args.Video, attempts).ConfigureAwait(false);

    if (_cancellationTokens.TryRemove(args.Video.ID, out var newCts))
      newCts.Dispose();
  }

  private async Task HandleReleaseSaved(VideoReleaseSavedEventArgs args)
  {
    if (!await _cachedData.IsFileTrackedAsync(args.Video.ID).ConfigureAwait(false)) return;

    if (_cancellationTokens.TryGetValue(args.Video.ID, out var cts))
    {
      await cts.CancelAsync().ConfigureAwait(false);
      _cancellationTokens.TryRemove(args.Video.ID, out _);
    }

    if (args.Video.Episodes.Count == 0 || args.Video.Series.Count == 0)
    {
      LogNeitherSeriesNorEpisodeIsDefinedForAMatchedVideo(args.Video, args.Video.Episodes, args.Video.Series);
      return; // We'll pick this up with an EpisodeUpdated event.
    }

    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();
    await discord.PatchMatchedWebhooks([args.Video], args.Video.Episodes[0], args.Video.Series[0])
      .ConfigureAwait(false);
  }

  private async Task HandleEpisodeAdded(EpisodeInfoUpdatedEventArgs args)
  {
    var episode = args.EpisodeInfo;
    var series = args.SeriesInfo;

    var trackedVideoIds =
      await _cachedData.GetTrackedFileIdsAsync(episode.VideoList.Select(v => v.ID)).ConfigureAwait(false);

    if (trackedVideoIds.Count == 0) return;

    var trackedVideos = episode.VideoList
      .Concat(series.Videos)
      .Where(v => trackedVideoIds.Contains(v.ID))
      .DistinctBy(v => v.ID)
      .ToList();

    if (trackedVideos.Count == 0) return;

    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();

    // N.B. We may not have a ShokoSeries or ShokoEpisode at this point! (When the series is first created/imported)
    await discord.PatchMatchedWebhooks(trackedVideos, episode, series).ConfigureAwait(false);
  }

  private async Task HandleAvDumpEvent(AvdumpEventArgs args)
  {
    using var scope = _scopeFactory.CreateScope();
    var discord = scope.ServiceProvider.GetRequiredService<DiscordService>();
    await discord.SendUnmatchedWebhooks(args.VideoIDs).ConfigureAwait(false);
  }

  private async Task HandleVideoFileDeleted(FileEventArgs args)
  {
    await _cachedData.DeleteMessageStateAsync(args.Video.ID).ConfigureAwait(false);
    await _cachedData.DeleteTrackedFilesAsync(args.Video.ID).ConfigureAwait(false);
  }

  #endregion HandleEvent
}
