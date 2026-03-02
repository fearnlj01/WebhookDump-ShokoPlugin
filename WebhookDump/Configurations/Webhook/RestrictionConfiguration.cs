using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;

namespace Shoko.Plugin.WebhookDump.Configurations.Webhook;

[Display(Name = "Webhook Restriction Settings")]
[Section]
public class RestrictionConfiguration
{
  [Display(Name = "Show Restricted Titles", Description = "Whether to show restricted titles in the webhook")]
  [DefaultValue(true)]
  public bool ShowRestrictedTitles { get; set; } = true;

  [Display(Name = "Post if Top Match Restricted",
    Description = "Whether to post the message at all if the top match is restricted")]
  [DefaultValue(true)]
  public bool PostIfTopMatchRestricted { get; set; } = true;

  public override string ToString()
  {
    return
      $"Restriction Configuration: ShowRestrictedTitles={ShowRestrictedTitles}, PostIfTopMatchRestricted={PostIfTopMatchRestricted}";
  }
}
