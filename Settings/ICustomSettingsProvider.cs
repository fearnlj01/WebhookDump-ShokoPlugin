namespace Shoko.Plugin.WebhookDump.Settings;

public interface ICustomSettingsProvider
{
	ICustomSettings GetSettings();

	void SaveSettings(ICustomSettings settings);

	void SaveSettings();
}