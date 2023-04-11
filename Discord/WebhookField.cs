using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class WebhookField : IWebhookField
{
  public string Name { get; set; }
  public string Value { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public bool? Inline { get; set; }
}
