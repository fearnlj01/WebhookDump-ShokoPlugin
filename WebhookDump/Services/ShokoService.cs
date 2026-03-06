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

    if (targetProvider.EnabledHashTypes.Add("CRC32"))
      videoHashingService.UpdateProviders(targetProvider);

    return Task.CompletedTask;
  }

  public async Task DumpFile(IVideo video)
  {
    await anidbService.AvdumpVideos(video).ConfigureAwait(false);
  }

  public async Task RescanFile(IVideo video, int matchAttempts = 1)
  {
    // We only want to rescan the file if it's already being tracked.
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
    LogSearchingForTitleTitle(logger, title);
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

  [LoggerMessage(LogLevel.Information, "Rescanning file (FileId={id},Attempt={attempts})")]
  static partial void LogRescanningFile(ILogger<ShokoService> logger, int id, int attempts);

  [LoggerMessage(LogLevel.Trace, "Searching for title: '{title}'")]
  static partial void LogSearchingForTitleTitle(ILogger<ShokoService> logger, string title);
}
