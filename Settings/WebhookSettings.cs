namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings : IWebhookSettings
{
  public string Url { get; set; } = "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}";

  public string Username { get; set; } = "Shoko";
  public string AvatarUrl { get; set; } = "https://shokoanime.com/icon.png";

  public string MessageText { get; set; }

  public string EmbedText { get; set; } = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.";
  public int EmbedColor { get; set; } = 0x3B82F6;
}