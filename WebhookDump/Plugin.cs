using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.WebhookDump.API;
using Shoko.Plugin.WebhookDump.Services;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump;

public class Plugin : IPlugin, IPluginServiceRegistration
{
  public string Name => "WebhookDump";

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
  }

  public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
  {
    new List<LoggingRule>
    {
      new() { Final = true, LoggerNamePattern = "Microsoft.Extensions.Http.DefaultHttpClientFactory" },
      new() { Final = true, LoggerNamePattern = "System.Net.Http.HttpClient.*" }
    }.ForEach(rule =>
    {
      rule.SetLoggingLevels(LogLevel.Trace, LogLevel.Info);
      LogManager.Configuration.LoggingRules.Insert(0, rule);
    });
    LogManager.ReconfigExistingLoggers();

    var fileCachePath = Path.Combine(applicationPaths.PluginsPath, "WebhookDump_FileCache.json");
    var messageCachePath = Path.Combine(applicationPaths.PluginsPath, "WebhookDump_MessageCache.json");

    serviceCollection.AddHttpClient<ShokoClient>().AddStandardResilienceHandler();
    serviceCollection.AddHttpClient<DiscordClient>().AddStandardResilienceHandler();

    serviceCollection
      .AddCustomSettings(applicationPaths)
      .AddSingleton<PersistentFileIdDict>(_ => new PersistentFileIdDict(fileCachePath))
      .AddSingleton<PersistentMessageDict>(_ => new PersistentMessageDict(messageCachePath))
      .AddHostedService<PersistentFileIdDict>(sp => sp.GetRequiredService<PersistentFileIdDict>())
      .AddHostedService<PersistentMessageDict>(sp => sp.GetRequiredService<PersistentMessageDict>())
      .AddHostedService<ReactionWatchService>()
      .AddActivatedSingleton<ShokoEventSubscriber>()
      .AddTransient<ShokoService>()
      .AddTransient<DiscordService>();
  }
}
