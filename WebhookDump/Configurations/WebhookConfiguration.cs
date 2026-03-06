using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Plugin.WebhookDump.Configurations.Webhook;

namespace Shoko.Plugin.WebhookDump.Configurations;

[Display(Name = "Discord", Description = " ")]
[Section(DisplaySectionType.Tab, ShowSaveAction = true)]
public class WebhookConfiguration
{
  [Visibility(Size = DisplayElementSize.Full)]
  [DefaultValue(true)]
  [Display(Name = "Enabled", Description = "Whether to enable webhook functionality")]
  public bool Enabled { get; set; } = true;

  [Url]
  [PasswordPropertyText]
  [Visibility(Size = DisplayElementSize.Full)]
  [DefaultValue("https://discord.com/api/webhooks/{webhook.id}/{webhook.token}")]
  [Display(Name = "Webhook URL", Description = "The URL provided by Discord to send webhook messages to")]
  public string WebhookUrl { get; set; } = "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}";

  [Url]
  [Visibility(Size = DisplayElementSize.Full)]
  [DefaultValue("http://localhost:8111")]
  [Display(Name = "Shoko 'Public' URL", Description = "The URL [& Port] that the Shoko server can be accessed at")]
  public string ShokoPublicUrl { get; set; } = "http://localhost:8111";

  [Visibility(Size = DisplayElementSize.Full)]
  [DefaultValue("Shoko")]
  [Display(Name = "Username", Description = "The username to use for the webhook")]
  public string Username { get; set; } = "Shoko";

  [Url]
  [Visibility(Size = DisplayElementSize.Full)]
  [DefaultValue("https://raw.githubusercontent.com/ShokoAnime/ShokoServer/refs/heads/master/.github/images/Shoko.png")]
  [Display(Name = "Avatar URL", Description = "The URL of the avatar to use for the webhook")]
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
}
