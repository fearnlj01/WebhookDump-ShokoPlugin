using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Models;
// Stolen straight from `Shoko.Server.API.v3.Models.Common`
public class AVDumpResult
{
  [Required] public string FullOutput { get; set; }

  [Required] public string Ed2k { get; set; }
}
