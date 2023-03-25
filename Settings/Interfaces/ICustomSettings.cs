namespace Shoko.Plugin.WebhookDump.Settings;
public interface ICustomSettings
{
	public ShokoSettings Shoko { get; }
	public WebhookSettings Webhook { get; }
}