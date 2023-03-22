namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings : IWebhookSettings
{
	public string WebhookUrl { get; set; }
	public string AvatarUrl { get; set; }
	public string WebhookShokoLink { get; set; }
}