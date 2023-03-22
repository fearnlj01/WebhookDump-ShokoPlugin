namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhook
{
	string Content { get; set; }
	WebhookEmbed[] Embeds { get; set; }
	string Username { get; set; }
	string AvatarUrl { get; set; }
}
