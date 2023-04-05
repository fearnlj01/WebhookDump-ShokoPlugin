namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookMessageSettings
{
	string MessageText { get; set; }
	string EmbedText { get; set; }
	string EmbedColor { get; set; }
}