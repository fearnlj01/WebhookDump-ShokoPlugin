using System.Collections.Generic;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public interface IWebhookEmbed
{
  string Title { get; set; }
  string Description { get; set; }
  string Url { get; set; }
  int Color { get; set; }
  IWebhookImage Thumbnail { get; set; }
  IWebhookFooter Footer { get; set; }
  List<WebhookField> Fields { get; set; }
}
