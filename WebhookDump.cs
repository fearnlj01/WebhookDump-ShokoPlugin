using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Settings;
using ISettingsProvider = Shoko.Plugin.WebhookDump.Settings.ISettingsProvider;

namespace Shoko.Plugin.WebhookDump;

public class WebhookDump : IPlugin
{
  private static readonly HttpClient _httpClient = new();

  public string Name => "WebhookDump";

  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

  private readonly ISettingsProvider _settingsProvider;

  private readonly ISettings _settings;

  private readonly HashSet<int> seenFiles = new();

  private readonly Dictionary<int, long> sentWebhooks = new();

  public static void ConfigureServices(IServiceCollection services)
  {
    services.AddSingleton<ISettingsProvider, SettingsProvider>();
    services.AddScoped<ISettings, CustomSettings>();
  }

  public WebhookDump(IShokoEventHandler eventHandler, ISettingsProvider settingsProvider)
  {
    eventHandler.FileNotMatched += OnFileNotMatched;
    eventHandler.FileMatched += OnFileMatched;
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();
  }

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
    _logger.Info($"Loaded (custom) settings without a string representation: {_settings}");
    #region TestData
    _  = "{\"id\":\"1092536396230705152\",\"type\":0,\"content\":\"\",\"channel_id\":\"1058301714924589096\",\"author\":{\"bot\":true,\"id\":\"1058302498122760233\",\"username\":\"Shoko\",\"avatar\":\"ef2254e105a3d1d4a9bfd6ad6c0935d8\",\"discriminator\":\"0000\"},\"attachments\":[],\"embeds\":[{\"type\":\"rich\",\"url\":\"http://localhost:8111/webui/utilities/unrecognized/files\",\"title\":\"[SuPlease] N1 - 12 80p) [D08C0F8D].mkv\",\"description\":\"The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.\",\"color\":3900150,\"fields\":[{\"name\":\"ED2K:\",\"value\":\"ed2k://|file|[SuPlease] N1 - 12 80p) [D08C0F8D].mkv|378382638|7601386EB3885661D5BFD370E7612E2D|/\",\"inline\":false},{\"name\":\"AniDB Link\",\"value\":\"[Fate/Stay Night](https://anidb.net/anime/3348/release/add)\",\"inline\":true},{\"name\":\"AniDB Link\",\"value\":\"[K-On!](https://anidb.net/anime/6257/release/add)\",\"inline\":true},{\"name\":\"AniDB Link\",\"value\":\"[City Hunter](https://anidb.net/anime/942/release/add)\",\"inline\":true}] }],\"mentions\":[],\"mention_roles\":[],\"pinned\":false,\"mention_everyone\":false,\"tts\":false,\"timestamp\":\"2023-04-03T19:49:35.206000+00:00\",\"edited_timestamp\":null,\"flags\":0,\"components\":[],\"webhook_id\":\"1058302498122760233\"}";
    #endregion
  }

  private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    var fileInfo = fileNotMatchedEvent.FileInfo;
    if (!IsProbablyAnime(fileInfo)) return;

    var matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    switch (matchAttempts)
    {
      case 1:
        seenFiles.Add(fileInfo.VideoFileID);
        var dumpResult = await DumpFile(fileInfo);

        _ = Task.Run(() => RescanFile(fileInfo, matchAttempts));

        var url = _settings.Webhook.Url;
        if (url == null || url == "https://discord.com/api/webhooks/{webhook.id}/{webhook.token}") break;

        var titleSearchResults = await AttemptTitleMatch(fileInfo);

        JsonSerializerOptions options = new()
        {
          PropertyNamingPolicy = new WebhookNamingPolicy()
        };
        var json = JsonSerializer.Serialize(new Webhook(_settingsProvider, fileInfo, dumpResult, titleSearchResults), options);

        try
        {
          HttpRequestMessage request = new(HttpMethod.Post, $"{url}?wait=true")
          {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
          };
          var response = await _httpClient.SendAsync(request);
          response.EnsureSuccessStatusCode();

          var content = await response.Content.ReadAsStringAsync();
          var jsonRoot = JsonDocument.Parse(content).RootElement;

          if (!jsonRoot.TryGetProperty("id", out var messageIdProp)) return;
          if (!long.TryParse(messageIdProp.GetString(), out var messageId)) return;
          sentWebhooks.Add(fileInfo.VideoFileID, messageId);
        }
        catch (HttpRequestException e)
        {
          _logger.Error("Webhook failed to send!", e);
        }
        break;
      case <= 5:
        if (!seenFiles.Contains(fileInfo.VideoFileID)) break;
        _ = Task.Run(() => RescanFile(fileInfo, matchAttempts));
        break;
      default:
        break;
    }
  }

  private async void OnFileMatched(object sender, FileMatchedEventArgs fileMatchedEventArgs)
  {

    var fileInfo = fileMatchedEventArgs.FileInfo;
    var shokoId = fileInfo.VideoFileID;
    if (!seenFiles.Remove(shokoId)) return;
    if (!sentWebhooks.TryGetValue(shokoId, out long webhookMessageId)) return;

    var episodeInfo = fileMatchedEventArgs.EpisodeInfo.FirstOrDefault();
    var animeInfo = fileMatchedEventArgs.AnimeInfo.FirstOrDefault();

    var episodeTitle = episodeInfo.Titles.Where(t => t.Language == TitleLanguage.English).FirstOrDefault();
    var episodeNum = episodeInfo.Number.ToString("00", CultureInfo.InvariantCulture);
    var shokoUrl = $"{_settings.Shoko.PublicUrl}:{_settings.Shoko.PublicPort}".TrimEnd(':');

    var json = $$"""{"content": null, "embeds": [{"title": "{{fileInfo.Filename}}", "description": "An unmatched file automatically dumped by this plugin has now been matched.", "url": "{{shokoUrl}}", "color": {{0x57f287}}, "fields": [{"name": "Entry", "value": "{{episodeNum}} - [{{episodeTitle.Title}}](https://anidb.net/e{{episodeInfo.EpisodeID}})", "inline": true }, {"name": "File ID", "value": "{{fileInfo.VideoFileID}}", "inline": true }, {"name": "Anime", "value": "[{{animeInfo.PreferredTitle}}](https://anidb.net/a{{animeInfo.AnimeID}})", "inline": false }]}], "username": "Shoko", "avatar_url": "{{_settings.Webhook.AvatarUrl}}", "attachments": []}""";

    var url = $"{_settings.Webhook.Url}/messages/{webhookMessageId}";
    HttpRequestMessage request = new(HttpMethod.Patch, url)
    {
      Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    try
    {
      var response = await _httpClient.SendAsync(request);
      var debugOutput = response.StatusCode;
      response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException)
    {
      _logger.Warn($"Couldn't edit webhook for file: \"{fileInfo.Filename}\"");
    }

    sentWebhooks.Remove(shokoId);
  }

  private async Task<AVDumpResult> DumpFile(IVideoFile file, int attemptCount = 1)
  {
    try
    {
      var settings = _settings.Shoko;
      HttpRequestMessage request = new(HttpMethod.Post, $"http://localhost:{settings.ServerPort}/api/v3/File/{file.VideoFileID}/AVDump")
      {
        Headers =
          {
            {"accept", "*/*"},
            {"apikey", settings.ApiKey }
          }
      };

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<AVDumpResult>(content);
    }
    catch (HttpRequestException e)
    {
      if (attemptCount < 3)
      {
        _logger.Warn($"Error automatically AVDumping file | Attempt {attemptCount} of 3", e);
        await Task.Delay(5000);
        return await DumpFile(file, attemptCount + 1);
      }
      else
      {
        _logger.Error($"Error automatically AVDumping file | Maximum retry attempts reached", e);
        return null;
      }
    }
  }

  private static bool IsProbablyAnime(IVideoFile file)
  {
    // TODO: There's a lot more regex checks that can probably be done here...
    //       Hopefully this is enough to filter out the worst of it at least
    var regex = new Regex(@"^(\[[^]]+\]).+\.mkv$");
    return file.FileSize > 100_000_000
      && regex.IsMatch(file.Filename);
  }

  private static string GetTitleFromFilename(IVideoFile file)
  {
    var filename = file.Filename;
    var regex = @"^((\[.*?\]\s*)*)(.+(?= - ))(.*)$";

    Match results = Regex.Match(filename, regex);
    if (results.Success)
    {
      return results.Groups[3].Value;
    }
    return file.Filename;
  }

  private async Task<AniDBSearchResult> AttemptTitleMatch(IVideoFile file)
  {
    try
    {
      var title = HttpUtility.UrlEncode(GetTitleFromFilename(file));
      var settings = _settings.Shoko;
      var uri = $"http://localhost:{settings.ServerPort}/api/v3/Series/AniDB/Search/{title}?includeTitles=false&pageSize=3&page=1";

      HttpRequestMessage request = new(HttpMethod.Get, uri)
      {
        Headers =
          {
            {"accept", "*/*"},
            {"apikey", settings.ApiKey }
          }
      };

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var responseContent = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<AniDBSearchResult>(responseContent);
    }
    catch (HttpRequestException e)
    {
      _logger.Warn("Unable to retrieve information about file from AniDB", e);
      return null;
    }
  }

  private async Task RescanFile(IVideoFile file, int autoMatchAttempts)
  {
    await Task.Delay(autoMatchAttempts * 5 * 60 * 1000);

    var settings = _settings.Shoko;
    var uri = $"http://localhost:{settings.ServerPort}/api/v3/File/{file.VideoFileID}/Rescan";

    try
    {
      await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, uri)
      {
        Headers =
      {
        {"accept", "*/*"},
        {"apikey", settings.ApiKey }
      }
      });
    }
    catch (HttpRequestException)
    {
      // replaceme: logging
    }
  }
}