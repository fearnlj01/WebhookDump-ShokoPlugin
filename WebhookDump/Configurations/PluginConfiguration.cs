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
    Full path to where older versions of the plugin kept their database, if it was moved from its default location.
    The plugin now keeps its data in the server's plugin database, and only reads this file once to carry its entries over.
    """
  )]
  public string? AlternativePluginDatabasePath { get; set; }
}
