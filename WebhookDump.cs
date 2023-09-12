using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Apis;
using Shoko.Plugin.WebhookDump.Models.AniDB;
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

  private readonly IMessageTracker _messageTracker;

  private readonly FileTracker fileTracker;

  public static void ConfigureServices(IServiceCollection services)
  {
    _ = services.AddSingleton<ISettingsProvider, SettingsProvider>()
                .AddScoped<ISettings, CustomSettings>()
                .AddSingleton<IShokoHelper, ShokoHelper>()
                .AddSingleton<IDiscordHelper, DiscordHelper>()
                .AddSingleton<IMessageTracker, MessageTracker>();
  }

  public WebhookDump(IShokoEventHandler eventHandler, ISettingsProvider settingsProvider, IShokoHelper shokoHelper, IDiscordHelper discordHelper, IMessageTracker messageTracker)
  {
    eventHandler.FileNotMatched += OnFileNotMatched;
    eventHandler.FileMatched += OnFileMatched;
    eventHandler.AVDumpEvent += OnAVDumpEvent;

    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    fileTracker = new();

    _shokoHelper = shokoHelper;
    _discordHelper = discordHelper;
    _messageTracker = messageTracker;
  }

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
    _logger.Info($"Loaded (custom) settings without a string representation: {_settings}");
  }

  public void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    IVideoFile fileInfo = fileNotMatchedEvent.FileInfo;
    int matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    if (!IsProbablyAnime(fileInfo) || fileNotMatchedEvent.HasCrossReferences)
    {
      return;
    }

    if (matchAttempts == 1)
    {
      fileTracker.TryAddFile(fileInfo);
      _ = Task.Run(() => _shokoHelper.DumpFile(fileInfo.VideoFileID)).ConfigureAwait(false); ;
    }

    if (
      matchAttempts <= _settings.Shoko.AutomaticMatch.MaxAttempts
      && _settings.Shoko.AutomaticMatch.Enabled
      && fileTracker.Contains(fileInfo.VideoFileID)
    )
    {
      try
      {
        _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        _logger.Warn("Exception: {ex}", ex);
      }
    }
  }

  private async void OnFileMatched(object sender, FileMatchedEventArgs fileMatchedEvent)
  {
    IVideoFile fileInfo = fileMatchedEvent.FileInfo;
    IAnime animeInfo = fileMatchedEvent.AnimeInfo.FirstOrDefault();
    IEpisode episodeInfo = fileMatchedEvent.EpisodeInfo.FirstOrDefault();

    if (!fileTracker.TryRemoveFile(fileInfo.VideoFileID))
    {
      return;
    }

    if (!_messageTracker.TryGetValue(fileInfo.VideoFileID, out string messageId))
    {
      return;
    }

    try
    {
      AniDBPoster poster = await _shokoHelper.GetSeriesPoster(animeInfo);
      MemoryStream imageStream = await _shokoHelper.GetImageStream(poster);

      await _discordHelper.PatchWebhook(fileInfo, animeInfo, episodeInfo, imageStream, messageId);
      _ = _messageTracker.TryRemoveMessage(fileInfo.VideoFileID);
    }
    catch (Exception ex)
    {
      _logger.Warn("Exception: {ex}", ex);
    }
  }

  private async void OnAVDumpEvent(object sender, AVDumpEventArgs dumpEvent)
  {
    if (dumpEvent.Type != AVDumpEventType.Success)
    {
      return;
    }

    for (int i = 0; i < dumpEvent.VideoIDs.Count; i++)
    {
      var ed2k = dumpEvent.ED2Ks[i];
      var fileId = dumpEvent.VideoIDs[i];

      if (!fileTracker.TryGetValue(fileId, out var file)) {
        continue;
      }

      AniDBSearchResult searchResult = await _shokoHelper.MatchTitle(file.Filename);
      string messageId = await _discordHelper.SendWebhook(file, ed2k, searchResult);

      _ = _messageTracker.TryAddMessage(file.VideoFileID, messageId);
    }
  }

  private static bool IsProbablyAnime(IVideoFile file)
  {
    Regex regex = new(@"^(\[[^]]+\]).+\.mkv$");
    return file.FileSize > 100_000_000
      && regex.IsMatch(file.Filename);
  }
}
