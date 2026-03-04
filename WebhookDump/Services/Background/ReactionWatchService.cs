using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shoko.Abstractions.Config;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Client;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Background;

public class ReactionWatchService(
  ICachedData messageCachedData,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  ShokoService shokoService,
  IServiceScopeFactory scopeFactory
) : BackgroundService
{
  private bool AttemptAutoMatch => pluginConfigurationProvider.Load().AutomaticMatch is
    { WatchReactions: true, Enabled: true };

  private static TimeSpan Interval => TimeSpan.FromMinutes(15);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        return;
      }

      if (!AttemptAutoMatch) continue;

      var messages = await messageCachedData.GetAllMessagesAsync().ConfigureAwait(false);
      if (messages.Count == 0) continue;

      using var scope = scopeFactory.CreateScope();
      var discordClient = scope.ServiceProvider.GetRequiredService<DiscordClient>();

      foreach (var (videoId, messageState) in messages)
      {
        var newState = await discordClient.GetWebhookMessageState(messageState.Id).ConfigureAwait(false);
        if (newState?.Reactions is null or { Count: 0 }) continue;

        var video = shokoService.GetVideoFromId(videoId);
        if (video is not null) await shokoService.RescanFile(video).ConfigureAwait(false);
      }
    }
  }
}
