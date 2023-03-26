using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Models;

public class Webhook : IWebhook
{
  private static CustomSettingsProvider _settingsProvider;
  private static WebhookSettings _settings;
  private static IVideoFile _videoFile;
  private static AVDumpResult _AVDumpResult;

  public Webhook(CustomSettingsProvider settingsProvider, IVideoFile file, AVDumpResult result)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings().Webhook;
    _videoFile = file;
    _AVDumpResult = result;

    Content = _settings.MessageText;
    Embeds = new[] { new WebhookEmbed(_settingsProvider, _videoFile, _AVDumpResult) };
    Username = _settings.Username;
    AvatarUrl = _settings.AvatarUrl;
  }

  public string Content { get; }
  public WebhookEmbed[] Embeds { get; }
  public string Username { get; }
  public string AvatarUrl { get; }
}