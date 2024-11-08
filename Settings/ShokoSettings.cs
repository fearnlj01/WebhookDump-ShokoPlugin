using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings
{
  [JsonPrivate]
  public string ApiKey { get; set; }

  [Required]
  [Range(1, 65536, ErrorMessage = "A server port of no more than 65536 may be set.")]
  public int ServerPort { get; set; } = 8111;

  [JsonPrivate]
  public string PublicUrl { get; set; } = "http://localhost";

  public int? PublicPort { get; set; }
  public AutomaticMatchSettings AutomaticMatch { get; set; } = new()
  {
    Enabled = true,
    MaxAttempts = 5,
    WatchReactions = false
  };
}
