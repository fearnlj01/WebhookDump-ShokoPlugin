using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookField : IWebhookField
{
	public string Name { get; set; }
	public string Value { get; set; }
}
