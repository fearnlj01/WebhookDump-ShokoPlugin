namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettings : ICustomSettings
{
	public ShokoSettings Shoko { get; set; }
	public WebhookSettings Webhook { get; set; }
}