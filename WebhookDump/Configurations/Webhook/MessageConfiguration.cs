using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations.Webhook;

[Display(Name = "Message Settings")]
[Section(DisplaySectionType.Minimal, ShowSaveAction = true)]
public class MessageConfiguration
{
  [TextArea]
  [Visibility(Size = DisplayElementSize.Small)]
  [DefaultValue(null)]
  [Display(Name = "Message Text",
    Description = """
                  The text to send as the webhook message.
                  Unless you want extra bulk in Discord messages, leave this blank.
                  """)]
  public string? MessageText { get; set; }

  [Visibility(Size = DisplayElementSize.Large)]
  [DefaultValue("")]
  [TextArea]
  [Display(Name = "Embed Text", Description = "The text to send in the webhook's embed")]
  public required string EmbedText { get; set; } = string.Empty;

  [RegularExpression("^#?(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "EmbedColor must be a valid hexadecimal value")]
  [DefaultValue("")]
  [Display(Name = "Embed Color", Description = "The color to use for the webhook's embed")]
  public required string EmbedColor { get; set; } = string.Empty;
}
