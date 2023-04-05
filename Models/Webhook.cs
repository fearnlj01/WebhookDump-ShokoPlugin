using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Models;

public class Webhook : IWebhook
{
  private static ISettingsProvider _settingsProvider;
  private static IWebhookSettings _settings;
  private static IVideoFile _videoFile;
  private static AVDumpResult _AVDumpResult;
  private static AniDBSearchResult _searchResult;

  public Webhook(ISettingsProvider settingsProvider, IVideoFile file, AVDumpResult result, AniDBSearchResult searchResult)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings().Webhook;

    _videoFile = file;
    _AVDumpResult = result;
    _searchResult = searchResult;

    Content = _settings.Unmatched.MessageText;
    Embeds = new[] { new WebhookEmbed(_settingsProvider, _videoFile, _AVDumpResult, _searchResult) };
    Username = _settings.Username;
    AvatarUrl = _settings.AvatarUrl;
  }

  public string Content { get; }
  public WebhookEmbed[] Embeds { get; }
  public string Username { get; }
  public string AvatarUrl { get; }
}