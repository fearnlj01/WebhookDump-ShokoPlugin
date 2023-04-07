namespace Shoko.Plugin.WebhookDump.Models.Discord;

public interface IWebhookAttachment
{
  int Id { get; set; }
  string Description { get; set; }
  string Filename { get; set; }
}