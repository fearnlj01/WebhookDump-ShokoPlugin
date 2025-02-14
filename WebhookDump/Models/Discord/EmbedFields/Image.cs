namespace Shoko.Plugin.WebhookDump.Models.Discord.EmbedFields;

public class Image
{
  public string Url { get; set; } = "attachment://unknown.jpg";
  public string? ProxyUrl { get; set; }
  public int? Height { get; set; }
  public int? Width { get; set; }
}
