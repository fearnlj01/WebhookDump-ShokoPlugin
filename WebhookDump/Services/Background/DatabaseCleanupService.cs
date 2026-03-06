using Microsoft.Extensions.Hosting;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Background;

public class DatabaseCleanupService(ICachedData cachedData) : BackgroundService
{
  private static TimeSpan Interval => TimeSpan.FromHours(12);
  private static TimeSpan CleanupInterval => TimeSpan.FromDays(14);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      await cachedData.CleanupOldEntriesAsync(CleanupInterval).WaitAsync(stoppingToken).ConfigureAwait(false);
      await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
    }
  }
}
