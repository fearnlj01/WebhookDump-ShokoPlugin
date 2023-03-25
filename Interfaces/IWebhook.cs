namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhook
{
	string Content { get; }
	WebhookEmbed[] Embeds { get; }
	string Username { get; }
	string AvatarUrl { get; }
}