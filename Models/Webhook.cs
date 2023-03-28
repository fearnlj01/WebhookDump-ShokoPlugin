using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Models;

public class Webhook : IWebhook
{
  private static CustomSettingsProvider _settingsProvider;
  private static WebhookSettings _settings;
  private static IVideoFile _videoFile;
  private static AVDumpResult _AVDumpResult;
  private static AniDBSearchResult _searchResult;

  public Webhook(CustomSettingsProvider settingsProvider, IVideoFile file, AVDumpResult result, AniDBSearchResult searchResult)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings().Webhook;
    _videoFile = file;
    _AVDumpResult = result;
    _searchResult = searchResult;

    Content = _settings.MessageText;
    Embeds = new[] { new WebhookEmbed(_settingsProvider, _videoFile, _AVDumpResult, _searchResult) };
    Username = _settings.Username;
    AvatarUrl = _settings.AvatarUrl;
  }

  public string Content { get; }
  public WebhookEmbed[] Embeds { get; }
  public string Username { get; }
  public string AvatarUrl { get; }

  // Add function to call /api/v3/Series/AniDB/Search/{query}
  // This searches the cache of titles known to Shoko
  #region Test names
    // [SubsPlease] Kyuuketsuki Sugu Shinu S2 - 12 (1080p) [AD7C01AF].mkv
    // [SubsPlease] Bocchi the Rock! - 01 (1080p) [E04F4EFB].mkv
    // [SubsPlease] Eiyuuou, Bu wo Kiwameru Tame Tenseisu - 11 (1080p) [8E2F5680].mkv
    // [SubsPlease] Fumetsu no Anata e S2 - 18 (1080p) [24DCFE76].mkv
    // [Beatrice-Raws] Fate stay night Heaven's Feel I - Presage Flower [BDRip 1920x1080 HEVC DTSHD].mkv
    // [Reaktor] Legend of the Galactic Heroes - E009 [720p][x265][10-bit].mkv
    // [Reaktor] Legend of the Galactic Heroes - Ginga Eiyuu Densetsu - Gaiden - Arc 5 - E14 [720p][x265][10-bit].mkv
    // [SubsPlease] Ijiranaide, Nagatoro-san S2 - 09 (1080p) [E82C9852].mkv
    // [SubsWhen][Isekai Ojisan][13][1080p][92F0CA69].mkv
    // [SubsPlease] Kami-tachi ni Hirowareta Otoko S2 - 12 (1080p) [8E7B8436].mkv
    // [SubsPlease] Tomo-chan wa Onnanoko! - 10v2 (1080p) [32391941].mkv
  #endregion
  // Only test names that don't pass is: "[SubsWhen][Isekai Ojisan][13][1080p][92F0CA69].mkv"... I don't blame
  // The more full regex below captures everything in 4x groups.
  // Group 1 is everything from the start in square brackets
  // Group 2 is the last matched set of square brackets in group 1
  // Group 3 is the target, file name
  // Group 4 is after the file name (assuming that " - " indicates a swap between series title and further information)
  // ^((\[.*?\]\s*)*)(.+(?= - ))(.*)$
  // Search this and return the top three results in the format of (to be corrected): [result.title](https://anidb.net/anime/{result.ID}/release/add)
  // Could also make a query to lookup the release date for an AniDB series to prefer more recent releases when matching?
}