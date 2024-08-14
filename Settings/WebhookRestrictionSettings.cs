using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings;

public class WebhookRestrictionSettings : IWebhookRestrictionSettings
{
  public bool ShowRestrictedTitles { get; set; }
  public bool PostIfTopMatchRestricted { get; set; }
}
