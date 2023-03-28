namespace Shoko.Plugin.WebhookDump.Models;

public interface IWebhookField
{
  string Name { get; }
  string Value { get; }
  bool Inline { get; }
}