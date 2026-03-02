using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Plugin.WebhookDump.Configurations.Webhook;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Webhook")]
[Section(DisplaySectionType.Tab)]
public class WebhookConfiguration : IConfiguration
{
  [Display(Name = "Enabled", Description = "Whether to enable webhook functionality")]
  [DefaultValue(2)]
  public bool Enabled { get; set; } = true;

  [Display(Name = "Webhook URL", Description = "The URL to send webhook requests to")]
  [DefaultValue("https://discord.com/api/webhooks/{webhook.id}/{webhook.token}")]
  public string WebhookUrl { get; set; } = "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}";

  [Display(Name = "Shoko Public URL", Description = "The URL [& Port] that the Shoko server can be accessed at")]
  [DefaultValue("http://localhost:8111")]
  public string ShokoPublicUrl { get; set; } = string.Empty;

  [Display(Name = "Username", Description = "The username to use for the webhook")]
  [DefaultValue("Shoko")]
  public string Username { get; set; } = "Shoko";

  [Display(Name = "Avatar URL", Description = "The URL of the avatar to use for the webhook")]
  [DefaultValue("https://raw.githubusercontent.com/ShokoAnime/ShokoServer/refs/heads/master/.github/images/Shoko.png")]
  public string AvatarUrl { get; set; } =
    "https://raw.githubusercontent.com/ShokoAnime/ShokoServer/refs/heads/master/.github/images/Shoko.png";

  [Display(Name = "Matched", Description = "The settings for matched file messages")]
  public MessageConfiguration Matched { get; set; } = new()
  {
    EmbedColor = "#57F287",
    EmbedText = "An unmatched file automatically dumped by this plugin has now been matched."
  };

  [Display(Name = "Unmatched", Description = "The settings for unmatched file messages")]
  public MessageConfiguration Unmatched { get; set; } = new()
  {
    EmbedColor = "#3B82F6",
    EmbedText =
      "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below."
  };

  [Display(Name = "Restrictions",
    Description = "The settings for restrictions on what can/cannot be sent to the webhook")]
  public RestrictionConfiguration Restrictions { get; set; } = new();

  public override string ToString()
  {
    return
      $"Webhook Configuration: Enabled={Enabled}, WebhookURL={(string.IsNullOrWhiteSpace(WebhookUrl) ? "" : "CENSORED")}, Username={Username}, AvatarURL={AvatarUrl}, Matched={Matched}, Unmatched={Unmatched}, Restrictions={Restrictions}";
  }
}
