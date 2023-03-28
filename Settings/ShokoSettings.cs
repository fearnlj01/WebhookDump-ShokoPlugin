using System.ComponentModel.DataAnnotations;
namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings : IShokoSettings
{
  [Required]
  public string ApiKey { get; set; } = "";

  [Range(1, 65536, ErrorMessage = "A server port of no more than 65536 may be set.")]
  public int ServerPort { get; set; } = 8111;

  public string PublicUrl { get; set; } = "http://localhost";
  public int? PublicPort { get; set; }
}