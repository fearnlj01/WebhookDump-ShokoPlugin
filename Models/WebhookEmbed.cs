using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Settings;
using System.Globalization;
using System.Text;
namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookEmbed : IWebhookEmbed
{
	private static CustomSettingsProvider _settingsProvider;
	private static CustomSettings _settings;
	private static IVideoFile _videoFile;
	private static AVDumpResult _AVDumpResult;

	public WebhookEmbed(CustomSettingsProvider customSettingsProvider, IVideoFile videoFile, AVDumpResult result)
	{
		_settingsProvider = customSettingsProvider;
		_settings = _settingsProvider.GetSettings();
		_videoFile = videoFile;
		_AVDumpResult = result;

		Title = _videoFile.Filename;
		Description = _settings.Webhook.EmbedText;
		Url = $"{_settings.Shoko.PublicUrl}:{_settings.Shoko.PublicPort?.ToString(CultureInfo.InvariantCulture)}".TrimEnd(':')
			+ "/webui/utilities/unrecognized/files";
		Color = _settings.Webhook.EmbedColor;
		Fields = new[] { new WebhookField(_AVDumpResult) };
	}

	public string Title { get; }
	public string Description { get; }
	public string Url { get; }
	public int Color { get; }
	public WebhookField[] Fields { get; }
}