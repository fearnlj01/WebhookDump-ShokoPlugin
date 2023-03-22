using System.Collections.Generic;

namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhook
{
	string Content { get; set; }
	List<IWebhookEmbed> Embeds { get; set; }
	string Username { get; set; }
	string AvatarUrl { get; set; }
}
