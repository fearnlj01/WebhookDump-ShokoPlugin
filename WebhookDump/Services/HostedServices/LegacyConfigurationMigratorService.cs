using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Configurations.Legacy;

namespace Shoko.Plugin.WebhookDump.Services.HostedServices;

public partial class LegacyConfigurationMigratorService(
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  IApplicationPaths applicationPaths,
  ILogger<LegacyConfigurationMigratorService> logger
) : IHostedService
{
  private const string SettingsFileName = "WebhookDump.json";
  private string V1SettingsFilePath => Path.Combine(applicationPaths.PluginsPath, SettingsFileName);
  private string V0SettingsFilePath => Path.Combine(applicationPaths.DataPath, SettingsFileName);

  public Task StartAsync(CancellationToken cancellationToken)
  {
    return File.Exists(V1SettingsFilePath)
      ? TryMigrateSettings(V1SettingsFilePath)
      : File.Exists(V0SettingsFilePath)
        ? TryMigrateSettings(V0SettingsFilePath)
        : Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private async Task TryMigrateSettings(string legacyFilePath)
  {
    LogMigrationStarted(logger, legacyFilePath);

    try
    {
      var fileText = await File.ReadAllTextAsync(legacyFilePath).ConfigureAwait(false);
      var legacySettings = JsonSerializer.Deserialize<LegacySettings>(fileText);

      var pluginConfiguration = pluginConfigurationProvider.Load();

      pluginConfiguration.Webhook.Enabled = legacySettings.Webhook.Enabled;
      pluginConfiguration.Webhook.WebhookUrl = legacySettings.Webhook.Url;
      pluginConfiguration.Webhook.Name = legacySettings.Webhook.Username;
      pluginConfiguration.Webhook.AvatarUrl = legacySettings.Webhook.AvatarUrl;

      pluginConfiguration.Webhook.Matched.MessageText = legacySettings.Webhook.Matched.MessageText;
      pluginConfiguration.Webhook.Matched.EmbedColor = legacySettings.Webhook.Matched.EmbedColor;
      pluginConfiguration.Webhook.Matched.EmbedText = legacySettings.Webhook.Matched.EmbedText ?? string.Empty;

      pluginConfiguration.Webhook.Unmatched.MessageText = legacySettings.Webhook.Unmatched.MessageText;
      pluginConfiguration.Webhook.Unmatched.EmbedColor = legacySettings.Webhook.Unmatched.EmbedColor;
      pluginConfiguration.Webhook.Unmatched.EmbedText = legacySettings.Webhook.Unmatched.EmbedText ?? string.Empty;

      pluginConfiguration.Webhook.Restrictions.ShowRestrictedTitles =
        legacySettings.Webhook.Restrictions.ShowRestrictedTitles;
      pluginConfiguration.Webhook.Restrictions.PostIfTopMatchRestricted =
        legacySettings.Webhook.Restrictions.PostIfTopMatchRestricted;

      pluginConfiguration.Webhook.ShokoPublicUrl =
        string.Concat(legacySettings.Shoko.PublicUrl, legacySettings.Shoko.PublicPort);

      pluginConfiguration.AutomaticMatching.Enabled = legacySettings.Shoko.AutomaticMatch.Enabled;
      pluginConfiguration.AutomaticMatching.MaxAttempts = legacySettings.Shoko.AutomaticMatch.MaxAttempts;
      pluginConfiguration.AutomaticMatching.WatchReactions = legacySettings.Shoko.AutomaticMatch.WatchReactions;

      pluginConfigurationProvider.Save();

      File.Delete(legacyFilePath);
    }
    catch (Exception ex)
    {
      LogFailedToMigrateSettingsFromFilepath(logger, ex, legacyFilePath);
      return;
    }

    LogMigrationComplete(logger, legacyFilePath);
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Information, "Migration started for WebhookDump's legacy settings from \"{FilePath}\".")]
  static partial void LogMigrationStarted(ILogger<LegacyConfigurationMigratorService> logger, string filePath);

  [LoggerMessage(LogLevel.Information, "Migration complete for WebhookDump's legacy settings from \"{FilePath}\".")]
  static partial void LogMigrationComplete(ILogger<LegacyConfigurationMigratorService> logger, string filePath);

  [LoggerMessage(LogLevel.Error, "Failed to migrate WebhookDump's legacy settings from \"{FilePath}\"!")]
  static partial void LogFailedToMigrateSettingsFromFilepath(ILogger<LegacyConfigurationMigratorService> logger,
    Exception ex, string filePath);

  #endregion
}
