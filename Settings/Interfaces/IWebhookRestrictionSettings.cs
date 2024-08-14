namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookRestrictionSettings
{
  public bool ShowRestrictedTitles { get; set; }
  public bool PostIfTopMatchRestricted { get; set; }
}
