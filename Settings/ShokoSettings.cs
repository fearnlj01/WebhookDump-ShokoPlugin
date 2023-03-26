using System.ComponentModel.DataAnnotations;
namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings : IShokoSettings
{
  [Required]
  public string ApiKey { get; } = "";

  [Range(1, 65536, ErrorMessage = "A server port of no more than 65536 may be set.")]
  public int ServerPort { get; } = 8111;

  public string PublicUrl { get; } = "http://localhost";
  public int? PublicPort { get; }
}