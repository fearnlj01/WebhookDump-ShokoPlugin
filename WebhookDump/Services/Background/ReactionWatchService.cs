using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Client;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services.Background;

public partial class ReactionWatchService(
  ICachedData messageCachedData,
  ILogger<ReactionWatchService> logger,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
  IServiceScopeFactory scopeFactory
) : BackgroundService
{
  private PluginConfiguration PluginConfiguration => pluginConfigurationProvider.Load();

  private bool AttemptAutoMatch => PluginConfiguration is
    { AutomaticMatching: { WatchReactions: true, Enabled: true }, Webhook.Enabled: true };

  private int MaxReactionScanAttempts => PluginConfiguration.AutomaticMatching.MaxReactionScanAttempts;

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

      if (AttemptAutoMatch)
        await CheckMessages().ConfigureAwait(false);
    }
  }

  private async Task CheckMessages()
  {
    LogMessageCheckStart(logger);

    var messages = await messageCachedData.GetAllMessagesAsync().ConfigureAwait(false);
    if (messages.Count == 0) return;

    LogCheckingMessageCount(logger, messages.Count);

    using var scope = scopeFactory.CreateScope();
    var discordClient = scope.ServiceProvider.GetRequiredService<DiscordClient>();
    var shokoService = scope.ServiceProvider.GetRequiredService<ShokoService>();

    foreach (var (videoId, messageState) in messages)
    {
      var newState = await discordClient.GetWebhookMessageState(messageState.Id).ConfigureAwait(false);
      if (newState?.Reactions is null or { Count: 0 }) continue;

      var video = shokoService.GetVideoFromId(videoId);
      if (video is null) continue;

      var matchAttempts = shokoService.GetVideoAnidbMatchAttemptCount(video);
      if (matchAttempts <= MaxReactionScanAttempts)
        await shokoService.RescanFile(video).ConfigureAwait(false);
    }

    LogMessageCheckFinished(logger);
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Trace, "Starting message reaction check")]
  static partial void LogMessageCheckStart(ILogger<ReactionWatchService> logger);

  [LoggerMessage(LogLevel.Trace, "Checking {Count} messages for reactions")]
  static partial void LogCheckingMessageCount(ILogger<ReactionWatchService> logger, int count);

  [LoggerMessage(LogLevel.Trace, "Finished checking messages for reactions")]
  static partial void LogMessageCheckFinished(ILogger<ReactionWatchService> logger);

  #endregion
}
