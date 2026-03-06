using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Automatic Matching", Description = " ")]
[Section(DisplaySectionType.Minimal, ShowSaveAction = true)]
public class AutomaticMatchConfiguration
{
  [Display(Name = "Enabled", Description = "If enabled, attempt to automatically match files")]
  [DefaultValue(true)]
  public bool Enabled { get; set; } = true;

  [Display(Name = "Max file rescan attempts",
    Description = "The maximum number of times a file will be automatically rescanned")]
  [DefaultValue(8)]
  public int MaxAttempts { get; set; } = 8;

  [Display(Name = "Watch reactions for rescan",
    Description =
      """
      If this **and** the Discord Webhook feature are both enabled, the plugin will check all the messages that it sends
      every fifteen minutes to see if they have any reactions. If a reaction is found, the plugin will attempt to rescan
      the corresponding file **indefinitely** until the file is matched, or the message is deleted on Discord.
      """)]
  [DefaultValue(false)]
  public bool WatchReactions { get; set; }
}
