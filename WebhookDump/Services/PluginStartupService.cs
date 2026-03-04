using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Persistence;
using Shoko.Plugin.WebhookDump.Services.Events;
using LogLevel = NLog.LogLevel;

namespace Shoko.Plugin.WebhookDump.Services;

public partial class PluginStartupService(
  IApplicationPaths applicationPaths,
  IShokoEventHandler shokoEventHandler,
  ICachedDataProxy cachedDataProxy,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  ILogger<PluginStartupService> logger,
  ShokoEventSubscriber shokoEventSubscriber,
  ShokoService shokoService
) : IHostedService
{
  // Included for the sake of initialization & suppressing warnings.
  private readonly ShokoEventSubscriber _shokoEventSubscriber = shokoEventSubscriber;
  private int _init;

  private static string PluginNamespace => typeof(Plugin).Namespace ?? string.Empty;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    shokoEventHandler.Started += OnServerStarted;
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  private void OnServerStarted(object? sender, EventArgs e)
  {
    if (Interlocked.Exchange(ref _init, 1) == 1) return;
    shokoEventHandler.Started -= OnServerStarted;

    InitDatabase();
    _ = shokoService.InitializeAsync();
    SuppressHttpClientLoggingNLog();
  }

  private void InitDatabase()
  {
    try
    {
      var pluginConfig = pluginConfigurationProvider.Load();
      var pluginConfigDirectory = Path.Combine(
        applicationPaths.ConfigurationsPath,
        UuidUtility.GetV5(typeof(Plugin).FullName!).ToString()
      );

      var defaultDbPath = Path.Combine(pluginConfigDirectory, "WebhookDump.db");
      var configuredPath = pluginConfig.AlternativePluginDatabasePath;

      var dbPath = string.IsNullOrWhiteSpace(configuredPath)
        ? defaultDbPath
        : Path.IsPathRooted(configuredPath)
          ? configuredPath
          : Path.Combine(pluginConfigDirectory, configuredPath); // Prevents bad user behaviour making the app sad

      Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? defaultDbPath);

      if (!cachedDataProxy.TrySetDatabase(new CachedData(dbPath)))
        LogReinitAttempt(logger, PluginNamespace);
    }
    catch (Exception ex)
    {
      LogUnableToInit(logger, PluginNamespace);
      cachedDataProxy.TrySetException(ex);
    }
  }

  private static void SuppressHttpClientLoggingNLog()
  {
    try
    {
      var config = LogManager.Configuration;
      if (config is null) return;

      const string ruleMarker = "WebhookDump.DropHttpClientFactoryLogging";
      if (config.Variables.ContainsKey(ruleMarker)) return;

      const string nullTargetName = "WebhookDump_Null";
      if (config.FindTargetByName(nullTargetName) is not NullTarget nullTarget)
      {
        nullTarget = new NullTarget { Name = nullTargetName };
        config.AddTarget(nullTarget);
      }

      var patterns = new[]
      {
        // AniDB logging is not the domain of this plugin but is enabled by virtue of the plugins actions :(
        "System.Net.Http.HttpClient.AniDB.*",
        "System.Net.Http.HttpClient.DiscordClient.*",
        "Microsoft.Extensions.Http.DefaultHttpClientFactory"
      };

      foreach (var pattern in patterns)
      {
        var rule = new LoggingRule(pattern, LogLevel.Trace, LogLevel.Warn, nullTarget) { Final = true };
        config.LoggingRules.Insert(0, rule);
      }

      config.Variables[ruleMarker] = "true";
      LogManager.Configuration = config;
      LogManager.ReconfigExistingLoggers();
    }
    catch
    {
      // It's only a logging error...
    }
  }

  [LoggerMessage(Microsoft.Extensions.Logging.LogLevel.Error, "Unable to correctly init the {pluginName} plugin!")]
  static partial void LogUnableToInit(ILogger<PluginStartupService> logger, string pluginName);

  [LoggerMessage(Microsoft.Extensions.Logging.LogLevel.Warning,
    "The {pluginName} plugin has already initialized its database, ignoring re-init request.")]
  static partial void LogReinitAttempt(ILogger<PluginStartupService> logger, string pluginName);
}
