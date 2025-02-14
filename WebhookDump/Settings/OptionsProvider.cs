using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.WebhookDump.Converters;

namespace Shoko.Plugin.WebhookDump.Settings;

public static class OptionsProvider
{
  private const string SettingsFileName = "WebhookDump.json";

  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

  private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

  private static readonly JsonSerializerOptions LoggerSerializerOptions = new()
    { WriteIndented = true, Converters = { new SkipPrivateAttributesConverter<Settings>() } };

  public static IServiceCollection AddCustomSettings(this IServiceCollection services,
    IApplicationPaths applicationPaths)
  {
    var settingsFilePath = Path.Combine(applicationPaths.PluginsPath, SettingsFileName);

    var oldSettingsLocation = Path.Combine(applicationPaths.ProgramDataPath, SettingsFileName);
    if (File.Exists(oldSettingsLocation))
      File.Move(oldSettingsLocation, settingsFilePath, true);

    EnsureSettingsFileExists(settingsFilePath);

    var configuration = new ConfigurationBuilder()
      .AddJsonFile(settingsFilePath, false, true)
      .Build();

    services.Configure<ShokoSettings>(configuration.GetSection("Shoko"));
    services.Configure<WebhookSettings>(configuration.GetSection("Webhook"));

    var stringRepresentation = JsonSerializer.Serialize(configuration.Get<Settings>(), LoggerSerializerOptions);
    Logger.Info(CultureInfo.InvariantCulture, "WebhookDump settings:\n{settings}", stringRepresentation);

    return services;
  }

  private static void EnsureSettingsFileExists(string settingsFilePath)
  {
    if (File.Exists(settingsFilePath)) return;

    var defaultSettings = new Settings(new ShokoSettings(), new WebhookSettings());

    var serializedSettings = JsonSerializer.Serialize(defaultSettings, SerializerOptions);
    File.WriteAllText(settingsFilePath, serializedSettings);
    Logger.Info("Initial WebhokDump settings file created.");
  }

  private sealed record Settings(ShokoSettings Shoko, WebhookSettings Webhook);
}
