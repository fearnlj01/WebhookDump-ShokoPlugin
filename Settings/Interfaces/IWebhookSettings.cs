namespace Shoko.Plugin.WebhookDump.Settings;

public interface IWebhookSettings
{
	public string Url { get; }

	public string Username { get; }
	public string AvatarUrl { get; }

	public string MessageText { get; }

	public string EmbedText { get; }
	public int EmbedColor { get; }
}