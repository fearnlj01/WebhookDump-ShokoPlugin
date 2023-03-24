namespace Shoko.Plugin.WebhookDump.Settings;

public interface IShokoSettings
{
	public string ApiKey { get; set; }
	public int ServerPort { get; set; }
}