using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class WebhookEmbed
{
  public string Title { get; set; }
  public string Description { get; set; }
  public string Url { get; set; }
  public int Color { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public WebhookImage Thumbnail { get; set; }
  public List<WebhookField> Fields { get; set; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public WebhookFooter Footer { get; set; }
}
