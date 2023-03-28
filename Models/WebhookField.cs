using System.Collections.Generic;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookField
{
	public IWebhookField[] Fields { get; }

	public WebhookField(WebhookDumpField dumpField, WebhookTitleField[] titleFields)
	{
    List<IWebhookField> webhookFields = new()
    {
      dumpField
    };
    webhookFields.AddRange(titleFields);
    Fields = webhookFields.ToArray();
  }

  public IWebhookField[] GetFields()
  {
    return Fields;
  }
}