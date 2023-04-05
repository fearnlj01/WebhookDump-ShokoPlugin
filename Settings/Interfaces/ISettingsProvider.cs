namespace Shoko.Plugin.WebhookDump.Settings;

public interface ISettingsProvider
{
  ISettings GetSettings();
  void SaveSettings(ISettings settings);
}
