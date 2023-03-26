namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettings : ICustomSettings
{
  public ShokoSettings Shoko { get; set; } = new();
  public WebhookSettings Webhook { get; set; } = new();
}