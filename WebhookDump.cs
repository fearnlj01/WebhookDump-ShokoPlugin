using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Apis;
using Shoko.Plugin.WebhookDump.Settings;
using ISettingsProvider = Shoko.Plugin.WebhookDump.Settings.ISettingsProvider;

namespace Shoko.Plugin.WebhookDump;

public class WebhookDump : IPlugin
{
  public string Name => "WebhookDump";

  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

  private readonly ISettingsProvider _settingsProvider;

  private readonly ISettings _settings;

  private readonly IShokoHelper _shokoHelper;

  private readonly IDiscordHelper _discordHelper;

  private readonly HashSet<int> seenFiles = new();

  private readonly Dictionary<int, string> sentWebhooks = new();

  public static void ConfigureServices(IServiceCollection services)
  {
    services.AddSingleton<ISettingsProvider, SettingsProvider>();
    services.AddScoped<ISettings, CustomSettings>();
    services.AddSingleton<IShokoHelper, ShokoHelper>();
    services.AddSingleton<IDiscordHelper, DiscordHelper>();
  }

  public WebhookDump(IShokoEventHandler eventHandler, ISettingsProvider settingsProvider, IShokoHelper shokoHelper, IDiscordHelper discordHelper)
  {
    eventHandler.FileNotMatched += OnFileNotMatched;
    eventHandler.FileMatched += OnFileMatched;

    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    _shokoHelper = shokoHelper;
    _discordHelper = discordHelper;
  }

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
    _logger.Info($"Loaded (custom) settings without a string representation: {_settings}");
    #region TestData
    _ = "{\"id\":\"1092536396230705152\",\"type\":0,\"content\":\"\",\"channel_id\":\"1058301714924589096\",\"author\":{\"bot\":true,\"id\":\"1058302498122760233\",\"username\":\"Shoko\",\"avatar\":\"ef2254e105a3d1d4a9bfd6ad6c0935d8\",\"discriminator\":\"0000\"},\"attachments\":[],\"embeds\":[{\"type\":\"rich\",\"url\":\"http://localhost:8111/webui/utilities/unrecognized/files\",\"title\":\"[SuPlease] N1 - 12 80p) [D08C0F8D].mkv\",\"description\":\"The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.\",\"color\":3900150,\"fields\":[{\"name\":\"ED2K:\",\"value\":\"ed2k://|file|[SuPlease] N1 - 12 80p) [D08C0F8D].mkv|378382638|7601386EB3885661D5BFD370E7612E2D|/\",\"inline\":false},{\"name\":\"AniDB Link\",\"value\":\"[Fate/Stay Night](https://anidb.net/anime/3348/release/add)\",\"inline\":true},{\"name\":\"AniDB Link\",\"value\":\"[K-On!](https://anidb.net/anime/6257/release/add)\",\"inline\":true},{\"name\":\"AniDB Link\",\"value\":\"[City Hunter](https://anidb.net/anime/942/release/add)\",\"inline\":true}] }],\"mentions\":[],\"mention_roles\":[],\"pinned\":false,\"mention_everyone\":false,\"tts\":false,\"timestamp\":\"2023-04-03T19:49:35.206000+00:00\",\"edited_timestamp\":null,\"flags\":0,\"components\":[],\"webhook_id\":\"1058302498122760233\"}";
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
        try
        {
          seenFiles.Add(fileInfo.VideoFileID);
          var dumpResult = await _shokoHelper.DumpFile(fileInfo);

          // Fire and forget a rescan event
          _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);

          // Exit now if not using webhooks
          if (!_settings.Webhook.Enabled) return;

          var searchResults = await _shokoHelper.MatchTitle(fileInfo);

          var messageId = await _discordHelper.SendWebhook(fileInfo, dumpResult, searchResults);
          sentWebhooks.Add(fileInfo.VideoFileID, messageId);
        }
        catch (Exception ex)
        {
          _logger.Warn("Exception: {ex}", ex);
        }
        break;
      case <= 5:
        try
        {
          if (!seenFiles.Contains(fileInfo.VideoFileID)) break;
          _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          _logger.Warn("Exception: {ex}", ex);
        }
        break;
      default:
        break;
    }
  }

  private async void OnFileMatched(object sender, FileMatchedEventArgs fileMatchedEvent)
  {
    var fileInfo = fileMatchedEvent.FileInfo;
    var animeInfo = fileMatchedEvent.AnimeInfo.FirstOrDefault();
    var episodeInfo = fileMatchedEvent.EpisodeInfo.FirstOrDefault();

    if (!seenFiles.Remove(fileInfo.VideoFileID)) return;
    if (!sentWebhooks.TryGetValue(fileInfo.VideoFileID, out string messageId)) return;

    try
    {
      var poster = await _shokoHelper.GetSeriesPoster(animeInfo);
      var imageStream = await _shokoHelper.GetImageStream(poster);

      await _discordHelper.PatchWebhook(fileInfo, animeInfo, episodeInfo, imageStream, messageId);
      sentWebhooks.Remove(fileInfo.VideoFileID);
    }
    catch (Exception ex)
    {
      _logger.Warn("Exception: {ex}", ex);
    }
  }

  private static bool IsProbablyAnime(IVideoFile file)
  {
    var regex = new Regex(@"^(\[[^]]+\]).+\.mkv$");
    return file.FileSize > 100_000_000
      && regex.IsMatch(file.Filename);
  }
}