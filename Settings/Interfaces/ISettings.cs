namespace Shoko.Plugin.WebhookDump.Settings;

public interface ISettings
{
  ShokoSettings Shoko { get; set; }
  WebhookSettings Webhook { get; set; }
}
