using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings.Webhook;

public class MessageSettings
{
  public string? MessageText { get; set; }
  public required string EmbedText { get; set; }

  [RegularExpression("^#?(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "EmbedColor must be a valid hexadecimal value")]
  public required string EmbedColor { get; set; }
}
