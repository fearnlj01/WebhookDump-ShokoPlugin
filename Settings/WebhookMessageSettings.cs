using System.ComponentModel;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookMessageSettings : IWebhookMessageSettings
{
	[DefaultValue(null)]
  public string MessageText { get; set; }

	[DefaultValue(null)]
  public string EmbedText { get; set; }

	[DefaultValue("#FFFFFF")]
  public string EmbedColor { get; set; }
}