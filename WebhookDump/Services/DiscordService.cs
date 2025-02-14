using System.Text;
using Microsoft.Extensions.Options;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Plugin.WebhookDump.API;
using Shoko.Plugin.WebhookDump.Exceptions;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Models.Discord;
using Shoko.Plugin.WebhookDump.Models.Discord.EmbedFields;
using Shoko.Plugin.WebhookDump.Models.Shoko.Enums;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Services;

// TODO: Implement error handling.
public class DiscordService(
  DiscordClient discord,
  ShokoService shoko,
  PersistentMessageDict cachedMessages,
  PersistentFileIdDict cachedFiles,
  IVideoService videoService,
  IOptionsMonitor<WebhookSettings> webhookOptionsMonitor,
  IOptionsMonitor<ShokoSettings> shokoOptionsMonitor)
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private WebhookSettings WebhookSettings => webhookOptionsMonitor.CurrentValue;
  private ShokoSettings ShokoSettings => shokoOptionsMonitor.CurrentValue;

  public async Task SendUnmatchedWebhooks(IReadOnlyCollection<int>? videoIds)
  {
    if (videoIds == null) return;
    foreach (var videoId in videoIds)
    {
      if (!cachedFiles.Contains(videoId)) continue;

      var video = videoService.GetVideoByID(videoId);
      if (video != null)
        await SendUnmatchedWebhook(video).ConfigureAwait(false);
    }
  }

  public async Task PatchMatchedWebhooks(ICollection<IVideo> videos, IEpisode episode, ISeries series)
  {
    foreach (var video in videos)
    {
      if (!cachedMessages.TryGetValue(video.ID, out var messageState) || messageState is null) continue;
      await PatchMatchedWebhook(messageState, video, episode, series).ConfigureAwait(false);
    }
  }

  private async Task PatchMatchedWebhook(MinimalMessageState messageState, IVideo video, IEpisode episode,
    ISeries series)
  {
    cachedFiles.Remove(video.ID);
    cachedMessages.Remove(video.ID);

    var posterStream = series.DefaultPoster?.GetStream();

    var message = CreateMatchedWebhook(video, episode, series, posterStream != null);

    await discord.PatchWebhook(messageState.Id, message, posterStream).ConfigureAwait(false);
  }

  private async Task SendUnmatchedWebhook(IVideo video)
  {
    var message = await CreateUnmatchedWebhookMessage(video).ConfigureAwait(false);
    if (message == null) return;

    var messageId = await discord.SendWebhook(message).ConfigureAwait(false);

    if (messageId is not null)
      cachedMessages.Add(video.ID, messageId);
  }

  private async Task<Message?> CreateUnmatchedWebhookMessage(IVideo video)
  {
    var embedBuilder = GetBaseEmbed(video, FileEventReason.Unmatched)
      .SetDescription(WebhookSettings.Unmatched.EmbedText)
      .AddField(new Field { Name = "ED2K", Value = ShokoService.GetSanitizedEd2K(video) });
    var webhookBuilder = GetBaseMessage(FileEventReason.Unmatched);

    try
    {
      var titles = await shoko.SearchForTitles(video).ConfigureAwait(false) ?? [];
      foreach (var series in titles)
        embedBuilder.AddField(new Field
        {
          Name = "AniDB Link",
          Value = $"[{series.Title}](https://anidb.net/anime/{series.ID}/release/add)",
          Inline = true
        });
    }
    catch (RestrictedSearchResultException)
    {
      Logger.Info(
        "Unable to create webhook message - Top match is restricted and the settings prevent this being posted.");
      return null;
    }

    if (video.Hashes.CRC != null && (!video.EarliestKnownName?.Contains(video.Hashes.CRC) ?? true))
      embedBuilder.SetColor("#D85311");

    var embed = embedBuilder.Build();
    return webhookBuilder.SetEmbeds(embed).Build();
  }

  private Message CreateMatchedWebhook(IVideo video, IEpisode episode, ISeries series, bool includeThumbnail)
  {
    var messageBuilder = GetBaseMessage(FileEventReason.Matched);
    var embedBuilder = GetBaseEmbed(video, FileEventReason.Matched)
      .SetDescription(WebhookSettings.Matched.EmbedText +
                      $"\nFile matched: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>")
      .SetFields([
        new Field
        {
          Name = "Anime",
          Value = $"[{series.PreferredTitle}](https://anidb.net/anime/{series.CrossReferences[0].AnidbAnimeID})",
          Inline = true
        },
        new Field
        {
          Name = "Episode",
          Value =
            $"{episode.EpisodeNumber} - [{episode.PreferredTitle}](https://anidb.net/episode/{episode.CrossReferences[0].AnidbEpisodeID})",
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
      .SetTitle(GetTitle(video.EarliestKnownName))
      .SetUrl(ShokoSettings.PublicUrl)
      .SetFooter(GetFooter(video))
      .SetColor(reason is FileEventReason.Matched
        ? WebhookSettings.Matched.EmbedColor
        : WebhookSettings.Unmatched.EmbedColor);
  }

  private Message.Builder GetBaseMessage(FileEventReason reason)
  {
    return Message.Create()
      .SetUsername(WebhookSettings.Username)
      .SetAvatarUrl(WebhookSettings.AvatarUrl)
      .SetContent(reason is FileEventReason.Matched
        ? WebhookSettings.Matched.MessageText
        : WebhookSettings.Unmatched.MessageText);
  }

  private static string GetTitle(string? title)
  {
    return StringHelper.EscapeMarkdownPairs(title ?? string.Empty);
  }

  private static Footer GetFooter(IVideo video)
  {
    var sb = new StringBuilder();
    var crc = video.Hashes.CRC ?? string.Empty;
    var crcMatch = video.EarliestKnownName?.Contains(crc) ?? false;

    sb.Append("File ID: ").Append(video.ID)
      .Append(" | ").Append("CRC: ").Append(crc);
    if (!crcMatch)
      sb.Append(" | ").Append("CRC not found in filename");

    return new Footer { Text = sb.ToString() };
  }
}
