using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shoko.Plugin.WebhookDump.Services;

public partial class SingletonInitializationService(
  IEnumerable<IInitializable> initializableServices,
  ILogger<SingletonInitializationService> logger
) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    LogWebhookdumpStartingInitializationOfCountSingletons(logger, initializableServices.Count());
    foreach (var service in initializableServices)
    {
      var serviceName = service.GetType().Name;
      LogWebhookdumpInitializingServiceName(logger, serviceName);
      await service.InitializeAsync(cancellationToken).ConfigureAwait(false);
      LogWebhookdumpServiceNameInitializedSuccessfully(logger, serviceName);
    }
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }

  [LoggerMessage(LogLevel.Information, "WebhookDump: Starting initialization of {count} singletons...")]
  static partial void LogWebhookdumpStartingInitializationOfCountSingletons(
    ILogger<SingletonInitializationService> logger, int count);

  [LoggerMessage(LogLevel.Trace, "WebhookDump: Initializing {serviceName}...")]
  static partial void LogWebhookdumpInitializingServiceName(ILogger<SingletonInitializationService> logger,
    string serviceName);

  [LoggerMessage(LogLevel.Trace, "WebhookDump: {serviceName} initialized successfully.")]
  static partial void LogWebhookdumpServiceNameInitializedSuccessfully(ILogger<SingletonInitializationService> logger,
    string serviceName);
}
