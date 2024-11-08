namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookSettings
{
  public bool Enabled { get; set; }

  [JsonPrivate]
  public string Url { get; set; }

  public string Username { get; set; } = "Shoko";

  public string AvatarUrl { get; set; } = "https://shokoanime.com/icon.png";

  public WebhookMessageSettings Matched { get; set; } = new()
  {
    EmbedColor = "#57F287",
    EmbedText = "An unmatched file automatically dumped by this plugin has now been matched.",
  };

  public WebhookMessageSettings Unmatched { get; set; } = new()
  {
    EmbedColor = "#3B82F6",
    EmbedText = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
  };

  public WebhookRestrictionSettings Restrictions { get; set; } = new()
  {
    ShowRestrictedTitles = false,
    PostIfTopMatchRestricted = true,
  };
}
