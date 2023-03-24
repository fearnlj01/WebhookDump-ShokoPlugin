using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookEmbed : IWebhookEmbed
{
	public string Title { get; set; }
	public string Description { get; set; }
	public string Url { get; set; }
	public int Color { get; set; }
	public WebhookField[] Fields { get; set; }
}
