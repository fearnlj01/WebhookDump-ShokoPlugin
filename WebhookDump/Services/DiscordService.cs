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

// TODO: Implement error handling.
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

  public async Task SendUnmatchedWebhooks(IReadOnlyCollection<int>? videoIds)
  {
    if (videoIds == null) return;
    foreach (var videoId in videoIds)
    {
      if (!await cachedData.IsFileTrackedAsync(videoId).ConfigureAwait(false)) continue;

      var video = shoko.GetVideoFromId(videoId);
      if (video is not null)
        await SendUnmatchedWebhook(video).ConfigureAwait(false);
    }
  }

  public async Task PatchMatchedWebhooks(ICollection<IVideo> videos, IEpisode episode, ISeries series)
  {
    foreach (var video in videos)
    {
      var messageState = await cachedData.GetMessageStateAsync(video.ID).ConfigureAwait(false);
      if (messageState is null) continue;

      await PatchMatchedWebhook(messageState, video, episode, series).ConfigureAwait(false);
    }
  }

  private async Task PatchMatchedWebhook(MinimalMessageState messageState, IVideo video, IEpisode episode,
    ISeries series)
  {
    var posterStream = series.DefaultPoster?.GetStream();

    var message = CreateMatchedWebhook(video, episode, series, posterStream != null);

    await discord.PatchWebhook(messageState.Id, message, posterStream).ConfigureAwait(false);
    await cachedData.DeleteEntryAsync(video.ID).ConfigureAwait(false);
  }

  private async Task SendUnmatchedWebhook(IVideo video)
  {
    var message = CreateUnmatchedWebhookMessage(video);
    if (message is null) return;

    var messageState = await discord.SendWebhook(message).ConfigureAwait(false);

    if (messageState is not null)
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
      LogUnableToCreateWebhookMessageTopMatchIsRestricted(logger);
      return null;
    }

    var crc = video.Crc32;
    if (!string.IsNullOrEmpty(crc) && (!video.EarliestKnownName?.Contains(crc) ?? true))
      embedBuilder.SetColor(CrcMismatchColor);

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
      .SetUsername(WebhookConfiguration.Username)
      .SetAvatarUrl(WebhookConfiguration.AvatarUrl)
      .SetContent(reason is FileEventReason.Matched
        ? WebhookConfiguration.Matched.MessageText
        : WebhookConfiguration.Unmatched.MessageText);
  }

  private static Footer GetFooter(IVideo video)
  {
    var sb = new StringBuilder();
    var crc = video.Crc32 ?? string.Empty;
    var crcMatch = video.EarliestKnownName?.Contains(crc) ?? false;

    sb.Append("File ID: ").Append(video.ID)
      .Append(" | ").Append("CRC: ").Append(crc);
    if (!crcMatch)
      sb.Append(" | ").Append("CRC not found in filename");

    return new Footer { Text = sb.ToString() };
  }

  [LoggerMessage(LogLevel.Information,
    "Unable to create webhook message - Top match is restricted and the settings prevent this being posted.")]
  static partial void LogUnableToCreateWebhookMessageTopMatchIsRestricted(ILogger<DiscordService> logger);
}
