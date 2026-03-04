using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Automatic Matching")]
[Section(DisplaySectionType.Tab)]
public class AutomaticMatchConfiguration
{
  [Display(Name = "Enabled", Description = "Controls if the plugin attempt to automatically match files that it dumps")]
  [DefaultValue(true)]
  public bool Enabled { get; set; } = true;

  [Display(Name = "Max file rescan attempts",
    Description = "The maximum number of times a file will be automatically rescanned")]
  [DefaultValue(8)]
  public int MaxAttempts { get; set; } = 8;

  [Display(Name = "Watch reactions for rescan",
    Description = """
                  Controls if the plugin attempts to 'watch' messages it sends for reactions.

                  If a reaction is detected, the plugin will attempt to rescan the file **indefinitely** until it is matched.

                  N.B. This requires Webhook Messages to be enabled.
                  """)]
  [DefaultValue(false)]
  public bool WatchReactions { get; set; }

  public override string ToString()
  {
    return
      $"Automatic Matching Configuration: Enabled={Enabled}, MaxAttempts={MaxAttempts}, WatchReactions={WatchReactions}";
  }
}
