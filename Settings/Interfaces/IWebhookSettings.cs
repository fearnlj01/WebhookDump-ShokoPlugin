namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookSettings
{
	bool Enabled { get; set; }
	string Url { get; set; }
	string Username { get; set; }
	string AvatarUrl { get; set; }
	WebhookMessageSettings Matched { get; set; }
	WebhookMessageSettings Unmatched { get; set; }
}