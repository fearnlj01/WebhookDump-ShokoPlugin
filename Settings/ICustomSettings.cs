namespace Shoko.Plugin.WebhookDump.Settings;
public interface ICustomSettings
{
	public ShokoSettings Shoko { get; set; }
	public WebhookSettings Webhook { get; set; }
}