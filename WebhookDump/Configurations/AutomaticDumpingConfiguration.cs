using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Automatic Dumping", Description = " ")]
[Section(DisplaySectionType.Minimal, ShowSaveAction = true)]
public class AutomaticDumpingConfiguration
{
  [DefaultValue(true)]
  [Display(Name = "Enabled", Description = "If enabled, allows the plugin to automatically AVDump unmatched files")]
  public bool Enabled { get; set; } = true;
}
