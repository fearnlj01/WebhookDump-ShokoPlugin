namespace Shoko.Plugin.WebhookDump.Settings;

public interface ICustomSettingsProvider
{
	CustomSettings GetSettings();
	void SaveSettings(CustomSettings settings);
}
