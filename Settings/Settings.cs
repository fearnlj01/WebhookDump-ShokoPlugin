using System.Text.Json;

namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettings
{
  public ShokoSettings Shoko { get; init; } = new();

  public WebhookSettings Webhook { get; init; } = new();

  public override string ToString()
  {
    var options = new JsonSerializerOptions
    {
      Converters = { new SkipPrivateAttributesConverter<CustomSettings>() },
      WriteIndented = true
    };
    return JsonSerializer.Serialize(this, options);
  }
}
