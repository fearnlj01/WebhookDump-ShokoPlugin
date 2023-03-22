using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookField : IWebhookField
{
	[JsonPropertyName("name")]	public string Name { get; set; }
	[JsonPropertyName("value")]	public string Value { get; set; }
}
