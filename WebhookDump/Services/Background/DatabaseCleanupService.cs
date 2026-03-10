using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Background;

public partial class DatabaseCleanupService(ICachedData cachedData, ILogger<DatabaseCleanupService> logger)
  : BackgroundService
{
  private static TimeSpan Interval => TimeSpan.FromHours(12);
  private static TimeSpan CleanupInterval => TimeSpan.FromDays(14);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(Interval);
    do
    {
      LogCleanupStarted(logger);
      await cachedData.CleanupOldEntriesAsync(CleanupInterval).WaitAsync(stoppingToken).ConfigureAwait(false);
      LogCleanupFinished(logger);
    } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
  }

  [LoggerMessage(LogLevel.Trace, Message = "Database cleanup started.")]
  static partial void LogCleanupStarted(ILogger logger);

  [LoggerMessage(LogLevel.Trace, Message = "Database cleanup finished.")]
  static partial void LogCleanupFinished(ILogger logger);
}
