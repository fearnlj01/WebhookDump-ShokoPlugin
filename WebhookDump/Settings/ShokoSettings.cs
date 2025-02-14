using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Shoko.Plugin.WebhookDump.Attributes;
using Shoko.Plugin.WebhookDump.Converters;
using Shoko.Plugin.WebhookDump.Settings.Shoko;

namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
    { WriteIndented = true, Converters = { new SkipPrivateAttributesConverter<ShokoSettings>() } };

  [JsonPrivate] public string ApiKey { get; set; } = string.Empty;

  [Range(1, 65536, ErrorMessage = "The port set must be between 1 and 65536")]
  public int ServerPort { get; set; } = 8111;

  [JsonPrivate] public string PublicUrl { get; set; } = "http://localhost";
  public int? PublicPort { get; set; }

  public AutomaticMatchSettings AutomaticMatch { get; set; } = new();

  public override string ToString()
  {
    return JsonSerializer.Serialize(this, SerializerOptions);
  }
}
