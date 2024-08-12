using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;
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

  private readonly FileTracker _fileTracker;

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

    _fileTracker = new();

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

  private void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    IVideoFile fileInfo = fileNotMatchedEvent.File;
    int matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    if (!IsProbablyAnime(fileInfo) || fileNotMatchedEvent.HasCrossReferences)
    {
      return;
    }

    if (matchAttempts == 1)
    {
      _fileTracker.TryAddFile(fileInfo);
      _ = Task.Run(() => _shokoHelper.DumpFile(fileInfo.VideoID)).ConfigureAwait(false);
    }

    if (
      matchAttempts <= _settings.Shoko.AutomaticMatch.MaxAttempts
      && _settings.Shoko.AutomaticMatch.Enabled
      && _fileTracker.Contains(fileInfo.VideoID)
    )
    {
      try
      {
        _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        _logger.Debug("Exception: {ex}", ex);
      }
    }
  }

  private async void OnFileMatched(object sender, FileEventArgs fileMatchedEvent)
  {
    IVideoFile fileInfo = fileMatchedEvent.File;

    if (fileMatchedEvent.Series.Count == 0 || fileMatchedEvent.Episodes.Count == 0)
    {
      // we don't want the plugin to panic here... so we'll just ignore that this ever happened.
      // This appears to happen for when a series is new to Shoko, the episode info is pulled after the XRefs are created and matched.
      return;
    }

    IShokoSeries animeInfo = fileMatchedEvent.Series[0];
    IShokoEpisode episodeInfo = fileMatchedEvent.Episodes[0];

    if (!_fileTracker.TryRemoveFile(fileInfo.VideoID))
    {
      return;
    }

    if (!_messageTracker.TryGetValue(fileInfo.VideoID, out string messageId))
    {
      return;
    }

    try
    {
      AniDBPoster poster = await _shokoHelper.GetSeriesPoster(animeInfo);
      MemoryStream imageStream = await _shokoHelper.GetImageStream(poster);

      await _discordHelper.PatchWebhook(fileInfo, animeInfo, episodeInfo, imageStream, messageId);
      _ = _messageTracker.TryRemoveMessage(fileInfo.VideoID);
    }
    catch (Exception ex)
    {
      _logger.Debug("Exception: {ex}", ex);
    }
  }

  private async void OnAVDumpEvent(object sender, AVDumpEventArgs dumpEvent)
  {
    if (dumpEvent.Type != AVDumpEventType.Success || !_settings.Webhook.Enabled)
    {
      return;
    }

    for (int i = 0; i < dumpEvent.VideoIDs.Count; i++)
    {
      var ed2k = dumpEvent.ED2Ks[i];
      var fileId = dumpEvent.VideoIDs[i];

      if (!_fileTracker.TryGetValue(fileId, out var file))
      {
        continue;
      }

      AniDBSearchResult searchResult = await _shokoHelper.MatchTitle(file.FileName);
      string messageId = await _discordHelper.SendWebhook(file, ed2k, searchResult);

      _ = _messageTracker.TryAddMessage(file.VideoID, messageId);
    }
  }

  private static bool IsProbablyAnime(IVideoFile file)
  {
    Regex regex = new(@"^(\[[^]]+\]).+\.mkv$");
    return file.Size > 100_000_000
      && regex.IsMatch(file.FileName);
  }
}
