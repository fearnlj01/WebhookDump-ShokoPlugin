namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookSettings
{
	bool Enabled { get; set; }
	string Url { get; set; }
	string Username { get; set; }
	string AvatarUrl { get; set; }
	IWebhookMessageSettings Matched { get; set; }
	IWebhookMessageSettings Unmatched { get; set; }
}