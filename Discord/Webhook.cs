using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class Webhook : IWebhook
{
  public string Content { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public IWebhookEmbed[] Embeds { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public IWebhookAttachment[] Attachments { get; set; }

  
  public string Username { get; set; }
  public string AvatarUrl { get; set; }
}