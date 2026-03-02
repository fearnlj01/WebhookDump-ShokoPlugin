namespace Shoko.Plugin.WebhookDump.Discord.Models.EmbedFields;

public class Field
{
  public required string Name { get; set; }
  public required string Value { get; set; }
  public bool? Inline { get; set; }
}
