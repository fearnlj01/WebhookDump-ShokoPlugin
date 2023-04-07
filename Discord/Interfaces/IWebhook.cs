namespace Shoko.Plugin.WebhookDump.Models.Discord;

public interface IWebhook
{
  string Content { get; set; }
  IWebhookEmbed[] Embeds { get; set; }
  IWebhookAttachment[] Attachments { get; set; }
  string Username { get; set; }
  string AvatarUrl { get; set; }
}