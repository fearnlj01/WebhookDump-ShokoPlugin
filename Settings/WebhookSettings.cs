namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings : IWebhookSettings
{
  public string Url { get; } = "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}";

  public string Username { get; } = "Shoko";
  public string AvatarUrl { get; } = "https://shokoanime.com/icon.png";

  public string MessageText { get; }

  public string EmbedText { get; } = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.";
  public int EmbedColor { get; } = 0x3B82F6;
}