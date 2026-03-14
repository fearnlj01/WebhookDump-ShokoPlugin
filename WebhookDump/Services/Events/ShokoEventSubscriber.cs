using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Video;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Events;

public partial class ShokoEventSubscriber : IDisposable
{
  private readonly ICachedData _cachedData;

  private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTokens = [];
  private readonly ILogger<ShokoEventSubscriber> _logger;
  private readonly IMetadataService _metadataService;
  private readonly ConfigurationProvider<PluginConfiguration> _pluginConfigurationProvider;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IVideoReleaseService _videoReleaseService;
  private readonly IVideoService _videoService;

  public ShokoEventSubscriber(
    IVideoService videoService,
    IVideoReleaseService videoReleaseService,
    IMetadataService metadataService,
    ICachedData cachedData,
    ILogger<ShokoEventSubscriber> logger,
    ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
    IServiceScopeFactory scopeFactory
  )
  {
    _cachedData = cachedData;
    _logger = logger;
    _videoService = videoService;
    _videoReleaseService = videoReleaseService;
    _scopeFactory = scopeFactory;
    _metadataService = metadataService;

    _pluginConfigurationProvider = pluginConfigurationProvider;

    _videoReleaseService.SearchCompleted += OnSearchCompleted;
    _videoReleaseService.ReleaseSaved += OnReleaseSaved;
    _videoService.VideoFileDeleted += OnVideoFileDeleted;
    _metadataService.EpisodeAdded += OnEpisodeAdded;
  }

  private PluginConfiguration PluginConfiguration => _pluginConfigurationProvider.Load();

  public void Dispose()
  {
    _videoReleaseService.SearchCompleted -= OnSearchCompleted;
    _videoReleaseService.ReleaseSaved -= OnReleaseSaved;
    _videoService.VideoFileDeleted -= OnVideoFileDeleted;
    _metadataService.EpisodeAdded -= OnEpisodeAdded;

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

  private async Task UseTransientServiceAsync<T>(Func<T, Task> action) where T : class
  {
    await using var asyncScope = _scopeFactory.CreateAsyncScope();
    var service = asyncScope.ServiceProvider.GetRequiredService<T>();
    await action(service).ConfigureAwait(false);
  }

  private void RunLoggedBackgroundTask(Task task, string operationName)
  {
    _ = task.ContinueWith(t =>
    {
      if (t.Exception is not null)
        LogBackgroundTaskException(_logger, operationName, t.Exception);
    }, TaskContinuationOptions.OnlyOnFaulted);
  }

  #region ProcessFeature

  private async Task TryProcessRescanAttempt(IVideo video, int matchAttempts)
  {
    var cts = _cancellationTokens.GetOrAdd(video.ID, _ => new CancellationTokenSource());

    // Subtract 30 seconds to try and preempt the AniDB UDP socket logging out
    var waitTime = TimeSpan.FromMinutes(5) * Math.Pow(2, matchAttempts - 1) - TimeSpan.FromSeconds(30);
    try
    {
      await Task.Delay(waitTime, cts.Token).ConfigureAwait(false);
      await UseTransientServiceAsync<ShokoService>(ss => ss.RescanFile(video, matchAttempts)).ConfigureAwait(false);
    }
    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
    {
    }
    finally
    {
      if (_cancellationTokens.TryRemove(video.ID, out var newCts) && ReferenceEquals(cts, newCts))
        newCts.Dispose();
    }
  }

  private async Task TryProcessNewWebhookMessage(IVideo video)
  {
    await UseTransientServiceAsync<DiscordService>(ds => ds.SendUnmatchedWebhook(video)).ConfigureAwait(false);
  }

  private async Task TryProcessUpdatedWebhookMessages(IList<IVideo> videos)
  {
    await UseTransientServiceAsync<DiscordService>(async ds =>
    {
      foreach (var video in videos)
        await ds.PatchMatchedWebhook(video, video.Episodes[0], video.Series[0]).ConfigureAwait(false);
    }).ConfigureAwait(false);
  }

  private async Task TryProcessUpdatedWebhookMessage(IVideo video)
  {
    await TryProcessUpdatedWebhookMessages([video]).ConfigureAwait(false);
  }

  #endregion

  #region OnEvent

  private void OnSearchCompleted(object? sender, VideoReleaseSearchCompletedEventArgs args)
  {
    RunLoggedBackgroundTask(HandleSearchCompleted(args), nameof(HandleSearchCompleted));
  }

  private void OnReleaseSaved(object? sender, VideoReleaseSavedEventArgs args)
  {
    RunLoggedBackgroundTask(HandleReleaseSaved(args), nameof(HandleReleaseSaved));
  }

  private void OnEpisodeAdded(object? sender, EpisodeInfoUpdatedEventArgs args)
  {
    RunLoggedBackgroundTask(HandleEpisodeAdded(args), nameof(HandleEpisodeAdded));
  }

  private void OnVideoFileDeleted(object? sender, FileEventArgs args)
  {
    RunLoggedBackgroundTask(HandleVideoFileDeleted(args), nameof(HandleVideoFileDeleted));
  }

  #endregion

  #region HandleEvent

  private async Task HandleSearchCompleted(VideoReleaseSearchCompletedEventArgs args)
  {
    if (args.IsSuccessful || args.AttemptedProviders.All(rp => rp.Name != "AniDB")) return;
    if (args.Video.CrossReferences.Any(x => x.AnidbAnime is not null)) return;
    if (args.Video.MediaInfo is null) return;

    // For as long as this plugin's core focus is matching against AniDB... This is the only provider we care for here.
    var matchAttempts = _videoReleaseService
      .GetReleaseMatchAttemptsForVideo(args.Video)
      .Count(ma => ma.AttemptedProviderNames.Contains("AniDB"));

    if (matchAttempts == 1)
    {
      await _cachedData.SaveTrackedFileAsync(args.Video.ID).ConfigureAwait(false);

      if (PluginConfiguration.AutomaticDumping.Enabled)
        await UseTransientServiceAsync<ShokoService>(ss => ss.DumpFile(args.Video)).ConfigureAwait(false);

      if (PluginConfiguration.Webhook.Enabled)
        await TryProcessNewWebhookMessage(args.Video).ConfigureAwait(false);
    }

    if (PluginConfiguration.AutomaticMatching.Enabled &&
        matchAttempts <= PluginConfiguration.AutomaticMatching.MaxAttempts)
      await TryProcessRescanAttempt(args.Video, matchAttempts).ConfigureAwait(false);
  }

  private async Task HandleReleaseSaved(VideoReleaseSavedEventArgs args)
  {
    if (!await _cachedData.IsFileTrackedAsync(args.Video.ID).ConfigureAwait(false)) return;

    if (_cancellationTokens.TryRemove(args.Video.ID, out var cts))
    {
      await cts.CancelAsync().ConfigureAwait(false);
      cts.Dispose();
    }

    if (!PluginConfiguration.Webhook.Enabled)
    {
      await _cachedData.DeleteEntryAsync(args.Video.ID).ConfigureAwait(false);
      return; // Early return as we only care for information if the webhook feature is enabled
    }

    if (args.Video.Episodes.Count == 0 || args.Video.Series.Count == 0)
    {
      LogVideoMatchedButNoSeriesOrEpisode(args.Video, args.Video.Episodes, args.Video.Series);
      return; // Avoid further operations now, we'll have to pick this up later in an EpisodeUpdated event
    }

    await TryProcessUpdatedWebhookMessage(args.Video).ConfigureAwait(false);
    await _cachedData.DeleteEntryAsync(args.Video.ID).ConfigureAwait(false);
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

    if (PluginConfiguration.Webhook.Enabled)
      await TryProcessUpdatedWebhookMessages(trackedVideos).ConfigureAwait(false);

    await _cachedData.DeleteEntriesAsync(trackedVideos.Select(v => v.ID)).ConfigureAwait(false);
  }

  private async Task HandleVideoFileDeleted(FileEventArgs args)
  {
    await _cachedData.DeleteEntryAsync(args.Video.ID).ConfigureAwait(false);
  }

  #endregion HandleEvent

  #region LoggerMessages

  [LoggerMessage(LogLevel.Error, "An exception occurred while running a background task. (Operation={operationName})")]
  static partial void LogBackgroundTaskException(ILogger<ShokoEventSubscriber> logger, string operationName,
    Exception ex);

  [LoggerMessage(LogLevel.Debug,
    "Neither series nor episode is defined for a matched video. (VideoId={video}, Episodes={episodes}, Series={series})")]
  partial void LogVideoMatchedButNoSeriesOrEpisode(IVideo video, IReadOnlyList<IShokoEpisode> episodes,
    IReadOnlyList<IShokoSeries> series);

  #endregion
}
