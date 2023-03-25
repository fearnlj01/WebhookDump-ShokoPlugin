namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookField : IWebhookField
{
	private static AVDumpResult _AVDumpResult;
	public WebhookField(AVDumpResult result)
	{
		_AVDumpResult = result;

		Name = "ED2K:";
		Value = _AVDumpResult.Ed2k;
	}

	public string Name { get; } 
	public string Value { get; }
}
