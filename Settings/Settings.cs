namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettings : ISettings
{
  public ShokoSettings Shoko { get; set; }

  public WebhookSettings Webhook { get; set; }

	public CustomSettings()
	{
    Shoko = new ShokoSettings();
    Webhook = new WebhookSettings();
  }
}