namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhookEmbed
{
  string Title { get; }
  string Description { get; }
  string Url { get; }
  int Color { get; }
  IWebhookField[] Fields { get; set; }
}