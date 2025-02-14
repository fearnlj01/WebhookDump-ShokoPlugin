using System.Globalization;
using Microsoft.Extensions.Options;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.API;
using Shoko.Plugin.WebhookDump.Exceptions;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Models.Shoko.Series;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Services;

// TODO: Implement error handling.
public class ShokoService(
  ShokoClient client,
  PersistentFileIdDict cachedFiles,
  IOptionsMonitor<WebhookSettings> webhookOptionsMonitor)
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private WebhookSettings WebhookSettings => webhookOptionsMonitor.CurrentValue;

  public async Task DumpFile(IVideo video)
  {
    Logger.Info(CultureInfo.InvariantCulture, "AVDumping file (FileId={id})", video.ID);
    cachedFiles.Add(video.ID, DateTimeOffset.UtcNow);
    await client.DumpFile(video.ID).ConfigureAwait(false);
  }

  public async Task RescanFile(IVideo video, int matchAttempts)
  {
    if (!cachedFiles.Contains(video.ID)) return;

    Logger.Info(CultureInfo.InvariantCulture, "Automatically rescanning file (FileId={id},Attempt={attempts})",
      video.ID, matchAttempts);
    await client.ScanFile(video.ID).ConfigureAwait(false);
  }

  public async Task<IList<AniDB>?> SearchForTitles(IVideo video)
  {
    var searchResult = await client.MatchTitle(video.EarliestKnownName ?? string.Empty).ConfigureAwait(false);
    if (searchResult is not { Total: > 0 }) return null;

    if (!WebhookSettings.Restrictions.PostIfTopMatchRestricted && searchResult.List[0].Restricted)
      throw new RestrictedSearchResultException(
        "Top match in title search is restricted and the webhook is configured to not be sent.");

    if (!WebhookSettings.Restrictions.ShowRestrictedTitles)
      searchResult.List.RemoveAll(series => series.Restricted);

    searchResult.List.Sort((a, b) =>
    {
      switch (a.IsCurrentlyAiring)
      {
        case true when b.IsCurrentlyAiring:
        {
          if (!a.AirDate.HasValue || !b.AirDate.HasValue) return 0; // Can't happen - just guarantees
          if (a.AirDate.Value == b.AirDate.Value) return 0;
          return a.AirDate > b.AirDate.Value ? -1 : 1;
        }
        case true:
          return -1;
        default:
          return b.IsCurrentlyAiring ? 1 : 0;
      }
    });

    return searchResult.List.Take(3).ToList();
  }

  public static string GetSanitizedEd2K(IVideo video)
  {
    var hash = video.Hashes.ED2K;
    var size = video.Size;
    var fileName = StringHelper.EscapeMarkdownPairs(video.EarliestKnownName ?? string.Empty);

    return $"ed2k://|file|{fileName}|{size}|{hash}|/";
  }
}
