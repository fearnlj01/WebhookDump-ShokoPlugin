using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class Webhook : IWebhook
{
	[JsonPropertyName("content")] public string Content { get; set; }
	[JsonPropertyName("embeds")] public WebhookEmbed[] Embeds { get; set; }
	[JsonPropertyName("username")] public string Username { get; set; }
	[JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; }
}