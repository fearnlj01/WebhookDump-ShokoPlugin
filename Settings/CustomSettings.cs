namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettings : ICustomSettings
{
  public ShokoSettings Shoko { get; } = new();
  public WebhookSettings Webhook { get; } = new();
}