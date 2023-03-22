using System.Collections.Generic;

namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhookEmbed
{
	string Title { get; set; }
	string Description { get; set; }
	string Url { get; set; }
	int Color { get; set; }
	List<IWebhookField> Fields { get; set; }
}
