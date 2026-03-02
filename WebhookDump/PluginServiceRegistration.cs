using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using NLog.Targets;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Utilities;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Client;
using Shoko.Plugin.WebhookDump.Extensions;
using Shoko.Plugin.WebhookDump.Persistence;
using Shoko.Plugin.WebhookDump.Services;
using Shoko.Plugin.WebhookDump.Services.Background;
using Shoko.Plugin.WebhookDump.Services.Events;

namespace Shoko.Plugin.WebhookDump;

public class PluginServiceRegistration : IPluginServiceRegistration
{
  public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
  {
    var dbPath = Path.Combine(applicationPaths.ConfigurationsPath,
      UuidUtility.GetV5(typeof(Plugin).FullName!).ToString(),
      "WebhookDump.db");

    serviceCollection.AddHttpClient<DiscordClient>();
    SuppressHttpClientLoggingNLog();

    serviceCollection
      .AddSingleton<Func<WebhookConfiguration>>(sp =>
        () => sp.GetRequiredService<IConfigurationService>().Load<WebhookConfiguration>())
      .AddSingleton<Func<AutomaticMatchConfiguration>>(sp =>
        () => sp.GetRequiredService<IConfigurationService>().Load<AutomaticMatchConfiguration>())
      .AddSingleton<ICachedData>(_ = new CachedData(dbPath))
      .AddInitializedSingleton<ShokoEventSubscriber>();

    serviceCollection
      .AddHostedService<DatabaseCleanupService>()
      .AddHostedService<ReactionWatchService>()
      .AddHostedService<SingletonInitializationService>();

    serviceCollection
      .AddTransient<ShokoService>()
      .AddTransient<DiscordService>();
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
        "System.Net.Http.HttpClient.*",
        "Microsoft.Extensions.Http.DefaultHttpClientFactory.*",
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
}
