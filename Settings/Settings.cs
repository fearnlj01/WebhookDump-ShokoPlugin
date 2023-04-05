namespace Shoko.Plugin.WebhookDump.Settings;

public class Settings : ISettings
{
  public IShokoSettings Shoko { get; set; }

  public IWebhookSettings Webhook { get; set; }

	public Settings()
	{
    Shoko = new ShokoSettings();
    Webhook = new WebhookSettings();
  }
}