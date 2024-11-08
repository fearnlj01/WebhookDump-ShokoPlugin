using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Models.Discord;
using Shoko.Plugin.WebhookDump.Models.Shoko.AniDB;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Apis;

public sealed class DiscordHelper : IDisposable
{
  private readonly HttpClient _httpClient;
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private readonly CustomSettings _settings;

  private readonly string _baseUrl;

  private readonly JsonSerializerOptions _options = new()
  {
    PropertyNamingPolicy = new WebhookNamingPolicy()
  };

  public DiscordHelper(SettingsProvider settingsProvider)
  {
    _settings = settingsProvider.GetSettings();

    _httpClient = new HttpClient();
    _baseUrl = _settings.Webhook.Url;
  }

  public async Task<string> SendWebhook(IVideo video, string dumpResult, AniDBSearchResult searchResult)
  {
    try
    {
      var webhook = GetUnmatchedWebhook(video, dumpResult, searchResult);

      Logger.Info(CultureInfo.InvariantCulture, "Sending Discord webhook (fileId={fileId})", video.ID);

      var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}?wait=true", webhook, _options);
      _ = response.EnsureSuccessStatusCode();

      await using var responseStream = await response.Content.ReadAsStreamAsync();
      using var jsonDoc = await JsonDocument.ParseAsync(responseStream);

      return jsonDoc.RootElement.GetProperty("id").GetString();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      Logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task PatchWebhook(IVideo video, ISeries anime, IEpisode episode, MemoryStream imageStream, string messageId)
  {
    Logger.Info(CultureInfo.InvariantCulture, "Attempting to update Discord message (fileId={fileId}, messageId={messageId})", video.ID, messageId);

    try
    {
      MultipartFormDataContent form = new();

      var webhook = GetMatchedWebhook(video, anime, episode);
      var json = JsonSerializer.Serialize(webhook, _options);

      form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");

      StreamContent imageStreamContent = new(imageStream)
      {
        Headers = {
          ContentType = MediaTypeHeaderValue.Parse("image/jpeg")
        }
      };

      form.Add(imageStreamContent, "files[0]", "unknown.jpg");

      var response = await _httpClient.PatchAsync($"{_baseUrl}/messages/{messageId}", form);

      _ = response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      Logger.Debug("Exception: {ex}", ex);
    }
  }

  private Webhook GetUnmatchedWebhook(IVideo video, string dumpResult, AniDBSearchResult searchResult)
  {
    UriBuilder publicUrl = new(_settings.Shoko.PublicUrl)
    {
      Port = _settings.Shoko.PublicPort ?? -1,
      Path = "/webui/utilities/unrecognized/files"
    };

    var crc = video.Hashes.CRC ?? string.Empty;
    var matchedCrc = video.EarliestKnownName?.Contains($"[{crc}]") ?? false;

    var colour = !matchedCrc ? "D85311" : _settings.Webhook.Unmatched.EmbedColor.TrimStart('#');

    return new Webhook()
    {
      Username = _settings.Webhook.Username,
      Content = _settings.Webhook.Unmatched.MessageText,
      AvatarUrl = _settings.Webhook.AvatarUrl,
      Embeds =
      [
        new WebhookEmbed
        {
          Title = video.EarliestKnownName,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Unmatched.EmbedText,
          Color = Convert.ToInt32(colour, 16),
          Fields = GetUnmatchedFields(dumpResult, searchResult),
          Footer = GetFooter(video)
        }
      ]
    };
  }

  private Webhook GetMatchedWebhook(IVideo video, ISeries anime, IEpisode episode)
  {
    UriBuilder publicUrl = new(_settings.Shoko.PublicUrl)
    {
      Port = _settings.Shoko.PublicPort ?? -1
    };

    return new Webhook()
    {
      Username = _settings.Webhook.Username,
      Content = _settings.Webhook.Matched.MessageText,
      AvatarUrl = _settings.Webhook.AvatarUrl,
      Embeds =
      [
        new WebhookEmbed
        {
          Title = video.EarliestKnownName,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Matched.EmbedText + $"\nFile matched: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>",
          Color = Convert.ToInt32(_settings.Webhook.Matched.EmbedColor.TrimStart('#'), 16),
          Fields = GetMatchedFields(anime, episode),
          Thumbnail = new WebhookImage
          {
            Url = "attachment://unknown.jpg"
          },
          Footer = GetFooter(video)
        }
      ],
      Attachments =
      [
        new WebhookAttachment
        {
          Id = 0,
          Description = "Anime Poster",
          Filename = "unknown.jpg"
        }
      ]
    };
  }

  private static List<WebhookField> GetUnmatchedFields(string dumpResult, AniDBSearchResult searchResult)
  {
    List<WebhookField> output =
    [
      new()
      {
        Name = "ED2K",
        Value = $"{dumpResult}"
      }
    ];
    output.AddRange(searchResult.List.Select(result => new WebhookField() { Name = "AniDB Link", Value = $"[{result.Title}](https://anidb.net/anime/{result.ID}/release/add)", Inline = true }));

    return output;
  }

  private static List<WebhookField> GetMatchedFields(ISeries series, IEpisode episode) {
    if (series.ShokoSeries.Count == 0)
    {
      // panic!
      return [];
    }

    return
    [
      new WebhookField
      {
      Name = "Anime",
      Value = $"[{series.PreferredTitle}](https://anidb.net/anime/{series.ShokoSeries[0].AnidbAnimeID})",
      Inline = true
      },
      new WebhookField
      {
        Name = "Episode",
        Value =
          $"{episode.EpisodeNumber} - [{episode.PreferredTitle}](https://anidb.net/episode/{episode.CrossReferences[0].AnidbEpisodeID})",
        Inline = true
      }
    ];
  }

  private static WebhookFooter GetFooter(IVideo video)
  {
    var crc = video.Hashes.CRC ?? string.Empty;
    var matchedCrc = video.EarliestKnownName?.Contains($"[{crc}]") ?? false;

    var sb = new StringBuilder();
    sb.Append("File ID: ").Append(video.ID);
    sb.Append(" | ").Append("CRC: ").Append(crc);

    if (!matchedCrc)
    {
      sb.Append(" | ").Append("CRC not found in filename");
    }

    return new WebhookFooter()
    {
      Text = sb.ToString()
    };
  }

  public async Task<bool> GetMessageReactionState(string messageId)
  {
    try
    {
      var response = await _httpClient.GetAsync($"{_baseUrl}/messages/{messageId}");
      _ = response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();

      using var jsonDoc = JsonDocument.Parse(content);
      return jsonDoc.RootElement.TryGetProperty("reactions", out _);
    }
    catch (Exception ex)
    {
      Logger.Warn($"Error retrieving details for message ID = '{messageId}'");
      Logger.Warn("Exception: {ex}", ex);
      return false;
    }
  }

  #region Disposal
  private bool _disposed;
  ~DiscordHelper()
  {
    Dispose(false);
  }
  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  private void Dispose(bool disposing)
  {
    if (_disposed) return;
    if (disposing)
    {
      _httpClient.Dispose();
    }
    _disposed = true;
  }
  #endregion
}
