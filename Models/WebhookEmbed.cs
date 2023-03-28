using System.Globalization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Settings;
namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookEmbed : IWebhookEmbed
{
  private static CustomSettingsProvider _settingsProvider;
  private static CustomSettings _settings;
  private static IVideoFile _videoFile;
  private static AVDumpResult _AVDumpResult;
  private static AniDBSearchResult _searchResult;

  public WebhookEmbed(CustomSettingsProvider customSettingsProvider, IVideoFile videoFile, AVDumpResult result, AniDBSearchResult searchResult)
  {
    _settingsProvider = customSettingsProvider;
    _settings = _settingsProvider.GetSettings();
    _videoFile = videoFile;
    _AVDumpResult = result;
    _searchResult = searchResult;

    Title = _videoFile.Filename;
    Description = _settings.Webhook.EmbedText;
    Url = $"{_settings.Shoko.PublicUrl}:{_settings.Shoko.PublicPort?.ToString(CultureInfo.InvariantCulture)}".TrimEnd(':')
      + "/webui/utilities/unrecognized/files";
    Color = _settings.Webhook.EmbedColor;

    var titleArray = new WebhookTitleField[_searchResult.List.Length];
    for (int i = 0; i < _searchResult.List.Length; i++)
    {
      titleArray[i] = new WebhookTitleField(_searchResult.List[i]);
    }

    Fields = new WebhookField(
      new WebhookDumpField(_AVDumpResult),
      titleArray
    ).GetFields();
  }

  public string Title { get; }
  public string Description { get; }
  public string Url { get; }
  public int Color { get; }
  public IWebhookField[] Fields { get; set; }
}