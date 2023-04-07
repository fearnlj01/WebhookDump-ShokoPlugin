namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class WebhookAttachment : IWebhookAttachment
{
  public int Id { get; set; }
  public string Description { get; set; }
  public string Filename { get; set; }
}