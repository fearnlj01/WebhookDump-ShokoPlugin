using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations.Webhook;

[Display(Name = "Message Restrictions")]
[Section(DisplaySectionType.Minimal, ShowSaveAction = true)]
public class RestrictionConfiguration
{
  [DefaultValue(true)]
  [Display(Name = "Show Restricted Titles", Description = "Whether to show restricted titles in the webhook")]
  public bool ShowRestrictedTitles { get; set; } = true;

  [DefaultValue(true)]
  [Display(Name = "Post if Top Match Restricted",
    Description = "Whether to post the message at all if the top match is restricted")]
  public bool PostIfTopMatchRestricted { get; set; } = true;
}
