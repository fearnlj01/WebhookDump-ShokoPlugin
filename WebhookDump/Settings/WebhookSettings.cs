using System.Text.Json;
using Shoko.Plugin.WebhookDump.Attributes;
using Shoko.Plugin.WebhookDump.Converters;
using Shoko.Plugin.WebhookDump.Settings.Webhook;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
    { WriteIndented = true, Converters = { new SkipPrivateAttributesConverter<WebhookSettings>() } };

  public bool Enabled { get; set; }

  [JsonPrivate] public string Url { get; set; } = "https://discord.com/api/webhooks/example/webhook";

  public string Username { get; set; } = "Shoko";

  public string AvatarUrl { get; set; } =
    "https://raw.githubusercontent.com/ShokoAnime/ShokoServer/refs/heads/master/.github/images/Shoko.png";

  public MessageSettings Matched { get; set; } = new()
  {
    EmbedColor = "#57F287",
    EmbedText = "An unmatched file automatically dumped by this plugin has now been matched."
  };

  public MessageSettings Unmatched { get; set; } = new()
  {
    EmbedColor = "#3B82F6",
    EmbedText =
      "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below."
  };

  public RestrictionSettings Restrictions { get; set; } = new();

  public override string ToString()
  {
    return JsonSerializer.Serialize(this, SerializerOptions);
  }
}
