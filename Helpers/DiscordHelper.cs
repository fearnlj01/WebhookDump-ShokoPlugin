using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
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

  public async Task<string> SendWebhook(IVideoFile file, AVDumpResult dumpResult, AniDBSearchResult searchResult)
  {
    try
    {
      var webhook = GetUnmatchedWebhook(file, dumpResult, searchResult);
      var json = JsonSerializer.Serialize(webhook, _options);

      var response = await _httpClient.PostAsync($"{BaseUrl}?wait=true", new StringContent(json, Encoding.UTF8, "application/json"));
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();

      using var jsonDoc = JsonDocument.Parse(content);
      return jsonDoc.RootElement.GetProperty("id").GetString();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      _logger.Warn("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task PatchWebhook(IVideoFile file, IAnime anime, IEpisode episode, MemoryStream imageStream, string messageId)
  {
    try
    {
      var form = new MultipartFormDataContent();

      var webhook = GetMatchedWebhook(file, anime, episode);
      var json = JsonSerializer.Serialize(webhook, _options);

      form.Add(new StringContent(json, Encoding.UTF8, "application/json"), "payload_json");

      var imageStreamContent = new StreamContent(imageStream)
      {
        Headers = {
          ContentType = MediaTypeHeaderValue.Parse("image/jpeg")
        }
      };

      form.Add(imageStreamContent, "files[0]", "unknown.jpg");

      var response = await _httpClient.PatchAsync($"{BaseUrl}/messages/{messageId}", form);

      response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      // TODO: More logging
      _logger.Warn("Exception: {ex}", ex);
    }
  }

  private Webhook GetUnmatchedWebhook(IVideoFile file, AVDumpResult dumpResult, AniDBSearchResult searchResult)
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
        new WebhookEmbed
        {
          Title = file.Filename,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Unmatched.EmbedText,
          Color = Convert.ToInt32(_settings.Webhook.Unmatched.EmbedColor.TrimStart('#'), 16),
          Fields = GetUnmatchedFields(dumpResult, searchResult),
          Footer = GetFooter(file)
        }
      }
    };
  }

  private Webhook GetMatchedWebhook(IVideoFile file, IAnime anime, IEpisode episode)
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
        new WebhookEmbed
        {
          Title = file.Filename,
          Url = publicUrl.Uri.ToString(),
          Description = _settings.Webhook.Matched.EmbedText,
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
        new WebhookAttachment
        {
          Id = 0,
          Description = "Anime Poster",
          Filename = "unknown.jpg"
        }
      }
    };
  }

  private static List<WebhookField> GetUnmatchedFields(AVDumpResult dumpResult, AniDBSearchResult searchResult)
  {
    var output = new List<WebhookField>()
    {
      new WebhookField()
      {
        Name = "ED2K",
        Value = $"{dumpResult.Ed2k}"
      }
    };

    foreach (var result in searchResult.List)
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

  private static List<WebhookField> GetMatchedFields(IAnime series, IEpisode episode)
  {
    var episodeTitle = episode.Titles.Where(t => t.Language == TitleLanguage.English).FirstOrDefault();
    var episodeNumber = episode.Number.ToString("00", CultureInfo.InvariantCulture);

    return new List<WebhookField>()
    {
      new WebhookField()
      {
        Name = "Anime",
        Value = $"[{series.PreferredTitle}](https://anidb.net/anime/{series.AnimeID})",
        Inline = true
      },
      new WebhookField()
      {
        Name = "Episode",
        Value = $"{episodeNumber} - [{episodeTitle.Title}](https://anidb.net/episode/{episode.EpisodeID})",
        Inline = true
      },
    };
  }

  private static WebhookFooter GetFooter(IVideoFile file)
  {
    return new WebhookFooter()
    {
      Text = $"File ID: {file.VideoFileID}"
    };
  }

  public async Task<bool> GetMessageReactionBool(string messageId)
  {
    try
    {
      var response = await _httpClient.GetAsync($"{BaseUrl}/messages/{messageId}");
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      
      using var jsonDoc = JsonDocument.Parse(content);
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
      if (disposing) _httpClient.Dispose();
      _disposed = true;
    }
  }
  #endregion
}