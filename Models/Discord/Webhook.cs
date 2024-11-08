using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class Webhook
{
  public string Content { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public WebhookEmbed[] Embeds { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public WebhookAttachment[] Attachments { get; set; }


  public string Username { get; set; }
  public string AvatarUrl { get; set; }
}
