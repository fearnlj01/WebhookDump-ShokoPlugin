using System.Collections.Generic;
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

namespace Shoko.Plugin.WebhookDump;

public class WebhookDump : IPlugin
{
  private static readonly HttpClient _httpClient = new();

  public string Name => "WebhookDump";

  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

  private readonly CustomSettingsProvider _settingsProvider;

  private readonly CustomSettings _settings;

  private readonly HashSet<int> seenFiles = new();

  public static void ConfigureServices(IServiceCollection services)
  {
    services.AddSingleton<ICustomSettingsProvider, CustomSettingsProvider>();
    services.AddScoped<ICustomSettings, CustomSettings>();
  }

  public WebhookDump(IShokoEventHandler eventHandler, ICustomSettingsProvider settingsProvider)
  {
    eventHandler.FileNotMatched += OnFileNotMatched;
    _settingsProvider = (CustomSettingsProvider)settingsProvider;
    _settings = _settingsProvider.GetSettings();
  }

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
    _logger.Info($"Loaded (custom) settings without a string representation: {_settings}");
  }

  private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    var fileInfo = fileNotMatchedEvent.FileInfo;
    if (!IsProbablyAnime(fileInfo))
    {
      return;
    }

    var matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    switch (matchAttempts)
    {
      case 1:
        seenFiles.Add(fileInfo.VideoFileID);
        var dumpResult = await DumpFile(fileInfo);

        _ = RescanFile(fileInfo, matchAttempts).ConfigureAwait(false);

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
          HttpRequestMessage request = new(HttpMethod.Post, url)
          {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
          };
          var response = await _httpClient.SendAsync(request);
          response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
          _logger.Error("Webhook failed to send!", e);
        }
        break;
      case <= 4:
        if (!seenFiles.Contains(fileInfo.VideoFileID)) break;
        await RescanFile(fileInfo, matchAttempts);
        break;
      default:
        break;
    }
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
    var uri = $"http://localhost:{settings.ServerPort}/api/v3/File{file.VideoFileID}/Rescan";

    try {
    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri)
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