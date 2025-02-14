namespace Shoko.Plugin.WebhookDump.Models.Discord.EmbedFields;

public class Field
{
  public required string Name { get; set; }
  public required string Value { get; set; }
  public bool? Inline { get; set; }
}
