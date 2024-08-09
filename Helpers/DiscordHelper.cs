using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Models.Discord;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Apis;

public class DiscordHelper : IDisposable, IDiscordHelper
{
  private readonly HttpClient _httpClient;
  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
  private readonly ISettings _settings;
  private readonly ISettingsProvider _settingsProvider;

  private readonly string BaseUrl;

  private readonly JsonSerializerOptions _options = new()
  {
    PropertyNamingPolicy = new WebhookNamingPolicy()
  };

  public DiscordHelper(ISettingsProvider settingsProvider)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    _httpClient = new();
    BaseUrl = _settings.Webhook.Url;
  }

  public async Task<string> SendWebhook(IVideoFile file, string dumpResult, AniDBSearchResult searchResult)
  {
    try
    {
      Webhook webhook = GetUnmatchedWebhook(file, dumpResult, searchResult);

      _logger.Info(CultureInfo.InvariantCulture, "Sending Discord webhook (fileId={fileId})", file.VideoID);

      HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{BaseUrl}?wait=true", webhook, _options);
      _ = response.EnsureSuccessStatusCode();

      using Stream responseStream = await response.Content.ReadAsStreamAsync();
      using JsonDocument jsonDoc = await JsonDocument.ParseAsync(responseStream);

      return jsonDoc.RootElement.GetProperty("id").GetString();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      _logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task PatchWebhook(IVideoFile file, IShokoSeries anime, IEpisode episode, MemoryStream imageStream, string messageId)
  {
    _logger.Info(CultureInfo.InvariantCulture, "Attempting to update Discord message (fileId={fileId}, messageId={messageId})", file.VideoID, messageId);

    try
    {
      MultipartFormDataContent form = new();

      Webhook webhook = GetMatchedWebhook(file, anime, episode);
      string json = JsonSerializer.Serialize(webhook, _options);

      form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");

      StreamContent imageStreamContent = new(imageStream)
      {
        Headers = {
          ContentType = MediaTypeHeaderValue.Parse("image/jpeg")
        }
      };

      form.Add(imageStreamContent, "files[0]", "unknown.jpg");

      HttpResponseMessage response = await _httpClient.PatchAsync($"{BaseUrl}/messages/{messageId}", form);

      _ = response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      _logger.Debug("Exception: {ex}", ex);
    }
  }

  private Webhook GetUnmatchedWebhook(IVideoFile file, string dumpResult, AniDBSearchResult searchResult)
  {
    UriBuilder publicUrl = new(_settings.Shoko.PublicUrl)
    {
      Port = _settings.Shoko.PublicPort ?? -1,
      Path = "/webui/utilities/unrecognized/files"
    };

    return new Webhook()
    {
      Username = _settings.Webhook.Username,
      Content = _settings.Webhook.Unmatched.MessageText,
      AvatarUrl = _settings.Webhook.AvatarUrl,
      Embeds = new WebhookEmbed[]
      {
        new() {
          Title = file.FileName,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Unmatched.EmbedText,
          Color = Convert.ToInt32(_settings.Webhook.Unmatched.EmbedColor.TrimStart('#'), 16),
          Fields = GetUnmatchedFields(dumpResult, searchResult),
          Footer = GetFooter(file)
        }
      }
    };
  }

  private Webhook GetMatchedWebhook(IVideoFile file, IShokoSeries anime, IEpisode episode)
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
      Embeds = new WebhookEmbed[]
      {
        new() {
          Title = file.FileName,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Matched.EmbedText + $"\nFile matched: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>",
          Color = Convert.ToInt32(_settings.Webhook.Matched.EmbedColor.TrimStart('#'), 16),
          Fields = GetMatchedFields(anime, episode),
          Thumbnail = new WebhookImage
          {
            Url = "attachment://unknown.jpg"
          },
          Footer = GetFooter(file)
        }
      },
      Attachments = new WebhookAttachment[]
      {
        new() {
          Id = 0,
          Description = "Anime Poster",
          Filename = "unknown.jpg"
        }
      }
    };
  }

  private static List<WebhookField> GetUnmatchedFields(string dumpResult, AniDBSearchResult searchResult)
  {
    List<WebhookField> output =
    new() {
      new WebhookField()
      {
        Name = "ED2K",
        Value = $"{dumpResult}"
      }
    };

    foreach (AniDBSeries result in searchResult.List)
    {
      output.Add(new WebhookField()
      {
        Name = "AniDB Link",
        Value = $"[{result.Title}](https://anidb.net/anime/{result.ID}/release/add)",
        Inline = true
      });
    }

    return output;
  }

  private static List<WebhookField> GetMatchedFields(IShokoSeries series, IEpisode episode)
  {
    AnimeTitle episodeTitle = episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.English);
    string episodeNumber = episode.EpisodeNumber.ToString("00", CultureInfo.InvariantCulture);

    return new() {
      new WebhookField()
      {
        Name = "Anime",
        Value = $"[{series.PreferredTitle}](https://anidb.net/anime/{series.ID})",
        Inline = true
      },
      new WebhookField()
      {
        Name = "Episode",
        Value = $"{episodeNumber} - [{episodeTitle.Title}](https://anidb.net/episode/{episode.ID})",
        Inline = true
      },
    };
  }

  private static WebhookFooter GetFooter(IVideoFile file)
  {
    return new WebhookFooter()
    {
      Text = $"File ID: {file.VideoID} | CRC: {file.Video?.Hashes.CRC}{(file.FileName.Contains($"[{file.Video?.Hashes.CRC}]") ? " | CRC in filename" : string.Empty)}"
    };
  }

  public async Task<bool> GetMessageReactionState(string messageId)
  {
    try
    {
      HttpResponseMessage response = await _httpClient.GetAsync($"{BaseUrl}/messages/{messageId}");
      _ = response.EnsureSuccessStatusCode();

      string content = await response.Content.ReadAsStringAsync();

      using JsonDocument jsonDoc = JsonDocument.Parse(content);
      return jsonDoc.RootElement.TryGetProperty("reactions", out _);
    }
    catch (Exception ex)
    {
      _logger.Warn($"Error retrieving details for message ID = '{messageId}'");
      _logger.Warn("Exception: {ex}", ex);
      return false;
    }
  }

  #region Disposal
  private bool _disposed;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      if (disposing)
      {
        _httpClient.Dispose();
      }

      _disposed = true;
    }
  }
  #endregion
}
