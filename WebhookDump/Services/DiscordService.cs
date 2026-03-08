using System.Text;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Client;
using Shoko.Plugin.WebhookDump.Discord.Models;
using Shoko.Plugin.WebhookDump.Discord.Models.EmbedFields;
using Shoko.Plugin.WebhookDump.Enums;
using Shoko.Plugin.WebhookDump.Exceptions;
using Shoko.Plugin.WebhookDump.Extensions;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Services;

public partial class DiscordService(
  DiscordClient discord,
  ShokoService shoko,
  ICachedData cachedData,
  ILogger<DiscordService> logger,
  ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider
)
{
  private WebhookConfiguration WebhookConfiguration => pluginConfigurationProvider.Load().Webhook;

  private static string CrcMismatchColor => "#D85311";

  public async Task PatchMatchedWebhook(IVideo video, IEpisode episode, ISeries series)
  {
    LogAttemptingToUpdateWebhook(logger, video.ID, episode.ID, series.ID);
    var messageState = await cachedData.GetMessageStateAsync(video.ID).ConfigureAwait(false);
    if (messageState is null) return;

    var posterStream = series.DefaultPoster?.GetStream();
    try
    {
      var message = CreateMatchedWebhook(video, episode, series, posterStream is not null);

      await discord.PatchWebhook(messageState.Id, message, posterStream).ConfigureAwait(false);
      LogWebhookUpdated(logger, video.ID, episode.ID, series.ID, messageState.Id);
    }
    catch (CrossReferenceException ex)
    {
      LogUnableToUpdateWebhookMessage(logger, video.ID, episode.ID, series.ID, messageState.Id, ex.Message);
    }
  }

  public async Task SendUnmatchedWebhook(IVideo video)
  {
    LogNewWebhookMessageAttempt(logger, video.ID);
    if (!await cachedData.IsFileTrackedAsync(video.ID).ConfigureAwait(false)) return;

    var message = CreateUnmatchedWebhookMessage(video);
    if (message is null) return;

    var messageState = await discord.SendWebhook(message).ConfigureAwait(false);

    if (messageState is null) return;

    LogNewWebhookMessageCreated(logger, video.ID, messageState.Id);
    await cachedData.SaveMessageStateAsync(video.ID, messageState).ConfigureAwait(false);
  }

  private Message? CreateUnmatchedWebhookMessage(IVideo video)
  {
    var embedBuilder = GetBaseEmbed(video, FileEventReason.Unmatched)
      .SetDescription(WebhookConfiguration.Unmatched.EmbedText)
      .AddField(new Field { Name = "ED2K", Value = video.MarkdownSanitizedEd2K });
    var webhookBuilder = GetBaseMessage(FileEventReason.Unmatched);

    try
    {
      var titles = shoko.SearchForTitles(video);
      if (titles.Count == 0) LogNoLikelyTitlesFound(logger, video.ID);

      foreach (var series in titles)
        embedBuilder.AddField(new Field
        {
          Name = "AniDB Link",
          Value = $"[{series.Title}](https://anidb.net/anime/{series.AnidbAnime?.ID}/release/add)",
          Inline = true
        });
    }
    catch (RestrictedSearchResultException)
    {
      LogNoWebhookMessageAsTopMatchIsRestricted(logger);
      return null;
    }

    var crc = video.Crc32;
    if (!video.HasCrc32InFilename)
    {
      embedBuilder.SetColor(CrcMismatchColor);
      LogCrcMismatch(logger, video.ID, crc, video.EarliestKnownName);
    }

    var embed = embedBuilder.Build();
    return webhookBuilder.SetEmbeds(embed).Build();
  }

  private Message CreateMatchedWebhook(IVideo video, IEpisode episode, ISeries series, bool includeThumbnail)
  {
    if (series.CrossReferences.Count == 0)
      throw new CrossReferenceException("Series has no cross references, cannot create matched webhook.");
    if (episode.CrossReferences.Count == 0)
      throw new CrossReferenceException("Episode has no cross references, cannot create matched webhook.");

    var messageBuilder = GetBaseMessage(FileEventReason.Matched);
    var embedBuilder = GetBaseEmbed(video, FileEventReason.Matched)
      .SetDescription(WebhookConfiguration.Matched.EmbedText +
                      $"\nFile matched: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>")
      .SetFields([
        new Field
        {
          Name = "Anime",
          Value = $"[{series.Title}](https://anidb.net/anime/{series.CrossReferences[0].AnidbAnimeID})",
          Inline = true
        },
        new Field
        {
          Name = "Episode",
          Value =
            $"{episode.EpisodeNumber} - [{episode.Title}](https://anidb.net/episode/{episode.CrossReferences[0].AnidbEpisodeID})",
          Inline = true
        }
      ]);

    if (includeThumbnail)
    {
      embedBuilder.SetThumbnail(new Image());
      messageBuilder.AddAttachment(new Attachment());
    }

    var embed = embedBuilder.Build();
    return messageBuilder.SetEmbeds(embed).Build();
  }

  private Embed.Builder GetBaseEmbed(IVideo video, FileEventReason reason)
  {
    return Embed.Create()
      .SetTitle(video.EarliestKnownName.EscapeMarkdownPairs())
      .SetUrl(WebhookConfiguration.ShokoPublicUrl)
      .SetFooter(GetFooter(video))
      .SetColor(reason is FileEventReason.Matched
        ? WebhookConfiguration.Matched.EmbedColor
        : WebhookConfiguration.Unmatched.EmbedColor);
  }

  private Message.Builder GetBaseMessage(FileEventReason reason)
  {
    return Message.Create()
      .SetUsername(WebhookConfiguration.Name)
      .SetAvatarUrl(WebhookConfiguration.AvatarUrl)
      .SetContent(reason is FileEventReason.Matched
        ? WebhookConfiguration.Matched.MessageText
        : WebhookConfiguration.Unmatched.MessageText);
  }

  private static Footer GetFooter(IVideo video)
  {
    var sb = new StringBuilder();

    sb.Append("File ID: ").Append(video.ID)
      .Append(" | ").Append("CRC: ").Append(video.Crc32);
    if (!video.HasCrc32InFilename)
      sb.Append(" | ").Append("CRC not found in filename");

    return new Footer { Text = sb.ToString() };
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Warning,
    "Unable to update webhook message (VideoId={VideoId},EpisodeId={EpisodeId},SeriesId={SeriesId},MessageId={MessageId}) Message=\n{ExceptionMessage}")]
  static partial void LogUnableToUpdateWebhookMessage(ILogger<DiscordService> logger, int videoId, int episodeId,
    int seriesId, ulong messageId, string exceptionMessage);

  [LoggerMessage(LogLevel.Information,
    "Unable to create webhook message - Top match is restricted and the settings prevent this being posted.")]
  static partial void LogNoWebhookMessageAsTopMatchIsRestricted(ILogger<DiscordService> logger);

  [LoggerMessage(LogLevel.Trace,
    "Attempting to update message for video. (VideoId={VideoId},EpisodeId={EpisodeId},SeriesId={SeriesId})")]
  static partial void LogAttemptingToUpdateWebhook(ILogger<DiscordService> logger, int videoId, int episodeId,
    int seriesId);


  [LoggerMessage(LogLevel.Trace,
    "Webhook message updated. (VideoId={VideoId},EpisodeId={EpisodeId},SeriesId={SeriesId},MessageId={MessageId})")]
  static partial void LogWebhookUpdated(ILogger<DiscordService> logger, int videoID, int episodeID, int seriesID,
    ulong messageId);

  [LoggerMessage(LogLevel.Trace, "Attempting to send new webhook message. (VideoId={VideoId})")]
  static partial void LogNewWebhookMessageAttempt(ILogger<DiscordService> logger, int videoId);

  [LoggerMessage(LogLevel.Trace, "Webhook message created. (VideoId={VideoId},MessageId={MessageId})")]
  static partial void LogNewWebhookMessageCreated(ILogger<DiscordService> logger, int videoId, ulong messageId);

  [LoggerMessage(LogLevel.Trace, "No likely titles found for video. (VideoId={VideoId})")]
  static partial void LogNoLikelyTitlesFound(ILogger<DiscordService> logger, int videoId);

  [LoggerMessage(LogLevel.Trace,
    "CRC32 not found in file title - Overriding Webhook embed colour. (VideoId={VideoId},Crc32={Crc32},EarliestKnownName={EarliestKnownName})")]
  static partial void LogCrcMismatch(ILogger<DiscordService> logger, int videoId, string? crc32,
    string? earliestKnownName);

  #endregion
}
