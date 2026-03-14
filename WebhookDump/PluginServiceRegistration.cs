using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Plugin.WebhookDump.Discord.Client;
using Shoko.Plugin.WebhookDump.Persistence;
using Shoko.Plugin.WebhookDump.Services;
using Shoko.Plugin.WebhookDump.Services.Background;
using Shoko.Plugin.WebhookDump.Services.Events;
using Shoko.Plugin.WebhookDump.Services.HostedServices;

namespace Shoko.Plugin.WebhookDump;

public class PluginServiceRegistration : IPluginServiceRegistration
{
  public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
  {
    serviceCollection
      .AddHttpClient<DiscordClient>();

    serviceCollection
      .AddSingleton<ICachedDataProxy, CachedDataProxy>()
      .AddSingleton<ICachedData>(sp => sp.GetRequiredService<ICachedDataProxy>())
      .AddSingleton<ShokoEventSubscriber>();

    serviceCollection
      .AddHostedService<PluginStartupService>()
      .AddHostedService<LegacyConfigurationMigratorService>()
      .AddHostedService<DatabaseCleanupService>()
      .AddHostedService<ReactionWatchService>();

    serviceCollection
      .AddTransient<ShokoService>()
      .AddTransient<DiscordService>();
  }
}
