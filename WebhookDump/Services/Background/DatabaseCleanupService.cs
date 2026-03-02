using Microsoft.Extensions.Hosting;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Background;

public class DatabaseCleanupService(ICachedData cachedData) : BackgroundService
{
  private static TimeSpan Interval => TimeSpan.FromHours(1);
  private static TimeSpan CleanupInterval => TimeSpan.FromDays(7);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      await cachedData.CleanupOldEntriesAsync(CleanupInterval).ConfigureAwait(false);
      await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
    }
  }
}
