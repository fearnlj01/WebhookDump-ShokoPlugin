using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Webhook Dump", Description = "Configuration for the Webhook Dump plugin")]
[Section(DisplaySectionType.Tab, DefaultSectionName = "Core settings")]
public class PluginConfiguration : IConfiguration
{
  public AutomaticMatchConfiguration AutomaticMatch { get; set; } = new();
  public WebhookConfiguration Webhook { get; set; } = new();

  [Display(Name = "Alternative Plugin Database Path",
    Description = """
                  Full path to an alternative location for the plugins database.
                  Please note that when changing this, the database will be created anew and not migrated.
                  """)]
  [Visibility(Advanced = true, Size = DisplayElementSize.Full)]
  public string? AlternativePluginDatabasePath { get; set; }
}
