namespace Shoko.Plugin.WebhookDump.Models.Discord;

public interface IWebhookField
{
  string Name { get; set; }
  string Value { get; set; }
  bool? Inline { get; set; }
}
