using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Apis;
using Shoko.Plugin.WebhookDump.Models;
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

  private readonly HashSet<int> seenFiles;

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

    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    seenFiles = new();

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

  private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    IVideoFile fileInfo = fileNotMatchedEvent.FileInfo;
    if (!IsProbablyAnime(fileInfo) || fileNotMatchedEvent.HasCrossReferences)
    {
      return;
    }

    int matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    if (matchAttempts == 1)
    {
      try
      {
        _ = seenFiles.Add(fileInfo.VideoFileID);
        AVDumpResult dumpResult = await _shokoHelper.DumpFile(fileInfo);

        if (_settings.Shoko.AutomaticMatch.Enabled)
        {
          _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);
        }

        // Exit now if not using webhooks
        if (!_settings.Webhook.Enabled)
        {
          return;
        }

        AniDBSearchResult searchResults = await _shokoHelper.MatchTitle(fileInfo);

        string messageId = await _discordHelper.SendWebhook(fileInfo, dumpResult, searchResults);
        _ = _messageTracker.TryAddMessage(fileInfo.VideoFileID, messageId);
      }
      catch (Exception ex)
      {
        _logger.Warn("Exception: {ex}", ex);
      }
      return;
    }

    if (matchAttempts <= _settings.Shoko.AutomaticMatch.MaxAttempts && _settings.Shoko.AutomaticMatch.Enabled)
    {
      try
      {
        if (!seenFiles.Contains(fileInfo.VideoFileID))
        {
          return;
        }

        _ = Task.Run(() => _shokoHelper.ScanFile(fileInfo, matchAttempts)).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        _logger.Warn("Exception: {ex}", ex);
      }
      return;
    }
  }

  private async void OnFileMatched(object sender, FileMatchedEventArgs fileMatchedEvent)
  {
    IVideoFile fileInfo = fileMatchedEvent.FileInfo;
    IAnime animeInfo = fileMatchedEvent.AnimeInfo.FirstOrDefault();
    IEpisode episodeInfo = fileMatchedEvent.EpisodeInfo.FirstOrDefault();

    if (!seenFiles.Remove(fileInfo.VideoFileID))
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
      System.IO.MemoryStream imageStream = await _shokoHelper.GetImageStream(poster);

      await _discordHelper.PatchWebhook(fileInfo, animeInfo, episodeInfo, imageStream, messageId);
      _ = _messageTracker.TryRemoveMessage(fileInfo.VideoFileID);
    }
    catch (Exception ex)
    {
      _logger.Warn("Exception: {ex}", ex);
    }
  }

  private static bool IsProbablyAnime(IVideoFile file)
  {
    Regex regex = new(@"^(\[[^]]+\]).+\.mkv$");
    return file.FileSize > 100_000_000
      && regex.IsMatch(file.Filename);
  }
}
