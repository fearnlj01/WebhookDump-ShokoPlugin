using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Automatic Matching")]
[Section(DisplaySectionType.Tab)]
public class AutomaticMatchConfiguration : IConfiguration
{
  [Display(Name = "Enabled", Description = "Whether automatic matching is enabled")]
  [DefaultValue(true)]
  public bool Enabled { get; set; } = true;

  [Display(Name = "Max file rescan attempts",
    Description = "The maximum number of times to automatically attempt to rescan a file")]
  [DefaultValue(8)]
  public int MaxAttempts { get; set; } = 8;

  [Display(Name = "Watch reactions for rescan",
    Description =
      "Whether to watch for reactions to trigger a rescan. N.B. This requires Webhook Messages to be enabled.")]
  [DefaultValue(false)]
  public bool WatchReactions { get; set; }

  public override string ToString()
  {
    return
      $"Automatic Matching Configuration: Enabled={Enabled}, MaxAttempts={MaxAttempts}, WatchReactions={WatchReactions}";
  }
}
