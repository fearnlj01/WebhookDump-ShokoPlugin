using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class Webhook : IWebhook
{
	public string Content { get; set; }
	public WebhookEmbed[] Embeds { get; set; }
	public string Username { get; set; }
	public string AvatarUrl { get; set; }
}