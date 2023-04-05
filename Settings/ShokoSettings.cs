using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings : IShokoSettings
{
  public string ApiKey { get; set; }

  [Required]
  [Range(1, 65536, ErrorMessage = "A server port of no more than 65536 may be set.")]
  public int ServerPort { get; set; }

  public string PublicUrl { get; set; }

  public int? PublicPort { get; set; }

  public ShokoSettings()
  {
    ApiKey = null;
    ServerPort = 8111;
    PublicUrl = "http://localhost";
    PublicPort = null;
  }
}