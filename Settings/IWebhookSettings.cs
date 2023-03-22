namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookSettings
{
	public string WebhookUrl { get; set; }
	public string AvatarUrl { get; set; }
	public string WebhookShokoLink { get; set; }
}