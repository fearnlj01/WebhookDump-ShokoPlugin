using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump
{
  public class WebhookDump : IPlugin
  {
    private static readonly HttpClient _httpClient = new();

    public string Name => "WebhookDump";

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly CustomSettingsProvider _settingsProvider;

    private readonly CustomSettings _settings;

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
      if (fileNotMatchedEvent.AutoMatchAttempts != 1 || !IsProbablyAnime(fileInfo))
      {
        return;
      }
      var result = await DumpFile(fileInfo);

      var url = _settings.Webhook.Url;
      if (url == null) return;

      JsonSerializerOptions options = new()
      {
        PropertyNamingPolicy = new WebhookNamingPolicy()
      };
      var json = JsonSerializer.Serialize(new Webhook(_settingsProvider, fileInfo, result), options);

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
          throw;
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
  }
}