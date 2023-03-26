namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookSettings
{
  public string Url { get; set; }

  public string Username { get; set; }
  public string AvatarUrl { get; set; }

  public string MessageText { get; set; }

  public string EmbedText { get; set; }
  public int EmbedColor { get; set; }
}