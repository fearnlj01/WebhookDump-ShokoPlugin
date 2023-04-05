using System.ComponentModel;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings : IWebhookSettings
{
	[DefaultValue(false)]
  public bool Enabled { get; set; }

	[DefaultValue(null)]
  public string Url { get; set; }

	[DefaultValue("Shoko")]
  public string Username { get; set; }

	[DefaultValue("https://shokoanime.com/icon.png")]
  public string AvatarUrl { get; set; }

  public IWebhookMessageSettings Matched { get; set; }

  public IWebhookMessageSettings Unmatched { get; set; }

	public WebhookSettings()
	{
    Matched = new WebhookMessageSettings()
		{
			EmbedColor = "#3B82F6",
			EmbedText = "An unmatched file automatically dumped by this plugin has now been matched.",
		};
		Unmatched = new WebhookMessageSettings()
		{
			EmbedColor = "#57F287",
			EmbedText = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
		};
  }
}