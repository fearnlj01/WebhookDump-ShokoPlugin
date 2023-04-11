using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookMessageSettings : IWebhookMessageSettings
{
  [DefaultValue(null)]
  public string MessageText { get; set; }

  [DefaultValue(null)]
  public string EmbedText { get; set; }

  [RegularExpression(@"^#?(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "EmbedColor must be a valid hexadecimal value")]
  [DefaultValue("#FFFFFF")]
  public string EmbedColor { get; set; }
}
