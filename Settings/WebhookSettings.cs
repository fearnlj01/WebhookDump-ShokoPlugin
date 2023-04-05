using System.ComponentModel;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings : IWebhookSettings
{
  public bool Enabled { get; set; }

  public string Url { get; set; }

  public string Username { get; set; }

  public string AvatarUrl { get; set; }

  public WebhookMessageSettings Matched { get; set; }

  public WebhookMessageSettings Unmatched { get; set; }

	public WebhookSettings()
	{
    Enabled = false;
    Url = null;
    Username = "Shoko";
    AvatarUrl = "https://shokoanime.com/icon.png";

    Matched = new WebhookMessageSettings()
		{
			EmbedColor = "#57F287",
			EmbedText = "An unmatched file automatically dumped by this plugin has now been matched.",
		};
		Unmatched = new WebhookMessageSettings()
		{
			EmbedColor = "#3B82F6",
			EmbedText = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
		};
  }
}