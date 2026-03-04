using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations.Webhook;

[Display(Name = "Webhook Message Settings")]
[Section]
public class MessageConfiguration
{
  [TextArea]
  [Display(Name = "Message Text",
    Description = """
                  The text to send as the webhook message.
                  Unless you want extra bulk in Discord messages, leave this blank.
                  """)]
  [Visibility(Size = DisplayElementSize.Small)]
  public string? MessageText { get; set; }

  [TextArea]
  [Display(Name = "Embed Text", Description = "The text to send in the webhook's embed")]
  [Visibility(Size = DisplayElementSize.Large)]
  [DefaultValue("")]
  public required string EmbedText { get; set; } = string.Empty;

  [RegularExpression("^#?(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "EmbedColor must be a valid hexadecimal value")]
  [Display(Name = "Embed Color", Description = "The color to use for the webhook's embed")]
  [DefaultValue("")]
  public required string EmbedColor { get; set; } = string.Empty;

  public override string ToString()
  {
    return $"Message Configuration: MessageText={MessageText}, EmbedText={EmbedText}, EmbedColor={EmbedColor}";
  }
}
