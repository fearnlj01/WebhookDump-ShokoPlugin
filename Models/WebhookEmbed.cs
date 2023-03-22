using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookEmbed : IWebhookEmbed
{
	[JsonPropertyName("title")]	public string Title { get; set; }
	[JsonPropertyName("description")]	public string Description { get; set; }
	[JsonPropertyName("url")]	public string Url { get; set; }
	[JsonPropertyName("color")]	public int Color { get; set; }
	[JsonPropertyName("fields")]	public WebhookField[] Fields { get; set; }
}
