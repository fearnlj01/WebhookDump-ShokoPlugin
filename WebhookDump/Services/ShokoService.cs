using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Video;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Configurations.Webhook;
using Shoko.Plugin.WebhookDump.Exceptions;
using Shoko.Plugin.WebhookDump.Extensions;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services;

public partial class ShokoService(
  ICachedData fileCachedData,
  IAnidbService anidbService,
  IVideoService videoService,
  IVideoHashingService videoHashingService,
  IVideoReleaseService videoReleaseService,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  ILogger<ShokoService> logger
)
{
  private RestrictionConfiguration RestrictionSettings =>
    pluginConfigurationProvider.Load().Webhook.Restrictions;

  public Task InitializeAsync()
  {
    var hashingProviders = videoHashingService.GetAvailableProviders();
    var hashTypes = videoHashingService.AllAvailableHashTypes;

    var targetProvider = hashingProviders.FirstOrDefault(p => p.Name == "Built-In Hasher");

    if (targetProvider is null || !hashTypes.Contains("CRC32")) return Task.CompletedTask; // Oh well...

    if (!targetProvider.EnabledHashTypes.Add("CRC32")) return Task.CompletedTask;

    LogCrc32HasherEnabled(logger);
    videoHashingService.UpdateProviders(targetProvider);

    return Task.CompletedTask;
  }

  public async Task DumpFile(IVideo video)
  {
    LogAvdumpScheduled(logger, video.ID);
    await anidbService.ScheduleAvdumpVideos(video).ConfigureAwait(false);
  }

  public async Task RescanFile(IVideo video, int matchAttempts = 1)
  {
    // Assert that we're already/still tracking the file before we rescan it.
    if (!await fileCachedData.IsFileTrackedAsync(video.ID).ConfigureAwait(false)) return;

    LogRescanningFile(logger, video.ID, matchAttempts);
    await videoReleaseService.ScheduleFindReleaseForVideo(video, true).ConfigureAwait(false);
  }

  public IVideo? GetVideoFromId(int videoId)
  {
    return videoService.GetVideoByID(videoId);
  }

  public IReadOnlyList<IAnidbAnimeSearchResult> SearchForTitles(IVideo video)
  {
    var title = video.EarliestKnownName.ExtractFileTitle();
    LogSearchingForTitle(logger, title);
    var searchResult = anidbService.Search(title, true);
    if (searchResult is not { Count: > 0 }) return searchResult;

    if (!RestrictionSettings.PostIfTopMatchRestricted && searchResult[0].IsRestricted)
      throw new RestrictedSearchResultException(
        "Top match in title search is restricted and the webhook is configured to not be sent.");

    var showRestricted = RestrictionSettings.ShowRestrictedTitles;

    var query = searchResult
      .Where(sr => sr.Distance <= 0.5)
      .Where(sr => showRestricted || !sr.IsRestricted)
      .OrderByDescending(sr => sr.ExactMatch)
      .ThenByDescending(sr => sr.IsCurrentlyAiring)
      .ThenByDescending(sr => sr.AirDate);

    return [.. query.Take(3)];
  }

  public int GetVideoAnidbMatchAttemptCount(IVideo video)
  {
    return videoReleaseService
      .GetReleaseMatchAttemptsForVideo(video)
      .Count(ma => ma.AttemptedProviderNames.Contains("AniDB"));
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Trace, "Scheduling file match (VideoId={VideoId},MatchAttempts={MatchAttempts})")]
  static partial void LogRescanningFile(ILogger<ShokoService> logger, int videoId, int matchAttempts);

  [LoggerMessage(LogLevel.Trace, "Searching for title: '{title}'")]
  static partial void LogSearchingForTitle(ILogger<ShokoService> logger, string title);

  [LoggerMessage(LogLevel.Trace, "Scheduling AVDump for video (VideoId={VideoId})")]
  static partial void LogAvdumpScheduled(ILogger<ShokoService> logger, int videoId);

  [LoggerMessage(LogLevel.Trace, "Enabling CRC32 hashing for built-in hasher provider.")]
  static partial void LogCrc32HasherEnabled(ILogger<ShokoService> logger);

  #endregion
}
