namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings : IShokoSettings
{
	public string ApiKey { get; set; }
	public string ServerPort { get; set; } = "8111";
}