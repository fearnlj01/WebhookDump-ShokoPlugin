namespace Shoko.Plugin.WebhookDump.Settings;

public interface ISettings
{
	IShokoSettings Shoko { get; set; }
	IWebhookSettings Webhook { get; set; }
}