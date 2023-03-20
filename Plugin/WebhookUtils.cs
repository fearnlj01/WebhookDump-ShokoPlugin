using System.Collections.Generic;

namespace Shoko.Plugin.WebhookDump.Utils;
#pragma warning disable IDE1006 // Naming rule violation
#pragma warning disable CA1707 // Naming rule violation (underscores)
public interface IWebhookField
{
	string name { get; set; }
	string value { get; set; }
}

public interface IWebhookEmbed
{
	string title { get; set; }	
	string description { get; set; }
	string url { get; set; }
	int color { get; set; }
	List<IWebhookField> fields { get; set; }
}

public interface IWebhook
{
	string content { get; set; }
	List<IWebhookEmbed> embeds { get; set; }
	string username { get; set; }
	string avatar_url { get; set; }
}
#pragma warning restore IDE1006 // Naming rule violation
#pragma warning restore CA1707 // Naming rule violation (underscores)

public class WebhookField : IWebhookField
{
	public string name { get; set;}
	public string value { get; set; }
}

public class WebhookEmbed : IWebhookEmbed
{
	public string title { get; set; }
	public string description { get; set; }
	public string url { get; set; }
	public int color { get; set; }
	public List<IWebhookField> fields { get; set; }
}

public class Webhook : IWebhook
{
	public string content { get; set; }
	public List<IWebhookEmbed> embeds { get; set; }
	public string username { get; set; }
	public string avatar_url { get; set; }
}