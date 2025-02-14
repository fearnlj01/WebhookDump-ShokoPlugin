using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shoko.Plugin.WebhookDump.API;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Services;

public class ReactionWatchService(
  PersistentMessageDict messageCache,
  IOptionsMonitor<ShokoSettings> settings,
  IServiceScopeFactory scopeFactory) : BackgroundService
{
  private bool AttemptAutoMatch => settings.CurrentValue.AutomaticMatch is { WatchReactions: true, Enabled: true };

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        return;
      }

      if (!AttemptAutoMatch) continue;

      using var scope = scopeFactory.CreateScope();
      var discordClient = scope.ServiceProvider.GetRequiredService<DiscordClient>();
      var shokoClient = scope.ServiceProvider.GetRequiredService<ShokoClient>();

      foreach (var (fileId, message) in messageCache.GetAll())
      {
        var newMessageState = await discordClient.GetWebhookMessageState(message.Id).ConfigureAwait(false);
        if (newMessageState?.Reactions is null or { Count: 0 }) continue;

        await shokoClient.ScanFile(fileId).ConfigureAwait(false);
      }
    }
  }
}
