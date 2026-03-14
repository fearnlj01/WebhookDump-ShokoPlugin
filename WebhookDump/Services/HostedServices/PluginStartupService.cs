using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Persistence;
using Shoko.Plugin.WebhookDump.Services.Events;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

namespace Shoko.Plugin.WebhookDump.Services.HostedServices;

public partial class PluginStartupService(
  IApplicationPaths applicationPaths,
  ISystemService systemService,
  ICachedDataProxy cachedDataProxy,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  ILogger<PluginStartupService> logger,
  ILogger<CachedData> cachedDataLogger,
  ShokoEventSubscriber shokoEventSubscriber,
  ShokoService shokoService
) : IHostedService
{
  // Included for the sake of initialization & suppressing warnings.
  private readonly ShokoEventSubscriber _shokoEventSubscriber = shokoEventSubscriber;
  private readonly TaskCompletionSource _startupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private Task? _startupTask;

  private static string PluginNamespace => typeof(Plugin).Namespace ?? string.Empty;

  public Task StartAsync(CancellationToken cancellationToken)
  {
    LogPluginWaitingForStart(logger);
    systemService.AboutToStart += OnServerAboutToStart;
    _startupTask = InitPluginAsync(cancellationToken);
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    systemService.AboutToStart -= OnServerAboutToStart;
    return Task.CompletedTask;
  }

  private void OnServerAboutToStart(object? sender, ServerAboutToStartEventArgs e)
  {
    systemService.AboutToStart -= OnServerAboutToStart;
    _startupTcs.TrySetResult();
  }

  private async Task InitPluginAsync(CancellationToken cancellationToken)
  {
    try
    {
      await _startupTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      InitDatabase();
      SuppressHttpClientLoggingNLog();

      try
      {
        await shokoService.InitializeAsync().ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        LogUnableToInitException(logger, ex, typeof(Plugin).FullName!);
      }

      LogStartupComplete(logger);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      LogStartupAborted(logger);
    }
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
          : Path.Combine(pluginConfigDirectory, configuredPath); // Prevents bad user behaviour making the plugin sad

      Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? pluginConfigDirectory);

      if (!cachedDataProxy.TrySetDatabase(new CachedData(dbPath, cachedDataLogger)))
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
        var rule = new LoggingRule(pattern, NLogLevel.Trace, NLogLevel.Warn, nullTarget) { Final = true };
        config.LoggingRules.Insert(0, rule);
      }

      config.Variables[ruleMarker] = "true";
      LogManager.Configuration = config;
      LogManager.ReconfigExistingLoggers();
    }
    catch
    {
      // Logging suppression is "best effort" only.
    }
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Information, "Plugin startup service started. Waiting for AboutToStart event.")]
  static partial void LogPluginWaitingForStart(ILogger<PluginStartupService> logger);

  [LoggerMessage(LogLevel.Error, "Unable to correctly init the {pluginName} plugin!")]
  static partial void LogUnableToInit(ILogger<PluginStartupService> logger, string pluginName);

  [LoggerMessage(LogLevel.Error, "Unable to correctly init the {pluginName} plugin!")]
  static partial void LogUnableToInitException(ILogger<PluginStartupService> logger, Exception ex, string pluginName);

  [LoggerMessage(LogLevel.Warning,
    "The {pluginName} plugin has already initialized its database, ignoring re-init request.")]
  static partial void LogReinitAttempt(ILogger<PluginStartupService> logger, string pluginName);

  [LoggerMessage(LogLevel.Trace, "Plugin startup complete")]
  static partial void LogStartupComplete(ILogger<PluginStartupService> logger);

  [LoggerMessage(LogLevel.Trace, "Server startup aborted, Plugin startup halting.")]
  static partial void LogStartupAborted(ILogger<PluginStartupService> logger);

  #endregion
}
