using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Webhook Dump", Description = "Configuration for the Webhook Dump plugin")]
[Section(DisplaySectionType.Tab, DefaultSectionName = "Advanced", AppendFloatingSectionsAtEnd = true)]
public class PluginConfiguration : IConfiguration
{
  public AutomaticDumpingConfiguration AutomaticDumping { get; set; } = new();
  public AutomaticMatchConfiguration AutomaticMatching { get; set; } = new();
  public WebhookConfiguration Webhook { get; set; } = new();

  [RequiresRestart]
  [Badge("Advanced Only", Theme = DisplayColorTheme.Danger)]
  [Visibility(Advanced = true, Size = DisplayElementSize.Full)]
  [DefaultValue(null)]
  [Display(Name = "Alternative Plugin Database Path", Description =
    """
    Full path to an alternative location for the plugins database.
    The plugin's database will **not** be migrated on change.
    """
  )]
  public string? AlternativePluginDatabasePath { get; set; }
}
