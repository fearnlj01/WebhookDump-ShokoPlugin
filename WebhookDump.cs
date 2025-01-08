using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.WebhookDump.Apis;
using Shoko.Plugin.WebhookDump.Settings;


namespace Shoko.Plugin.WebhookDump;

public class WebhookDump : IPlugin
{
  public string Name => "WebhookDump";

  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

  private readonly CustomSettings _settings;

  private readonly ShokoHelper _shokoHelper;

  private readonly DiscordHelper _discordHelper;

  private readonly MessageTracker _messageTracker;

  private readonly FileTracker _fileTracker;

  private readonly HashSet<int> _anidbTrackedEpisodeIds = [];

  public static void ConfigureServices(IServiceCollection services)
  {
    _ = services.AddSingleton<SettingsProvider>()
                .AddScoped<CustomSettings>()
                .AddSingleton<ShokoHelper>()
                .AddSingleton<DiscordHelper>()
                .AddSingleton<MessageTracker>();
  }

  public WebhookDump(IShokoEventHandler eventHandler, SettingsProvider settingsProvider, ShokoHelper shokoHelper, DiscordHelper discordHelper, MessageTracker messageTracker)
  {
    eventHandler.FileNotMatched += OnFileNotMatched;
    eventHandler.FileMatched += OnFileMatched;
    eventHandler.AVDumpEvent += OnAVDumpEvent;
    eventHandler.EpisodeUpdated += OnEpisodeUpdatedEvent;

    _settings = settingsProvider.GetSettings();

    _fileTracker = new FileTracker();

    _shokoHelper = shokoHelper;
    _discordHelper = discordHelper;
    _messageTracker = messageTracker;
  }

  public void OnSettingsLoaded(IPluginSettings settings)
  {
  }

  public void Load()
  {
    Logger.Info("Settings::\n{_settings}", _settings);
  }

  private void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
  {
    var video = fileNotMatchedEvent.Video;
    var matchAttempts = fileNotMatchedEvent.AutoMatchAttempts;

    if (video.MediaInfo == null || fileNotMatchedEvent.HasCrossReferences)
    {
      return;
    }

    if (matchAttempts == 1)
    {
      _fileTracker.TryAddFile(video);
      _ = Task.Run(() => _shokoHelper.DumpFile(video.ID)).ConfigureAwait(false);
    }

    if (matchAttempts > _settings.Shoko.AutomaticMatch.MaxAttempts
        || !_settings.Shoko.AutomaticMatch.Enabled
        || !_fileTracker.Contains(video.ID)) return;

    try
    {
      _ = WaitForRescan(video, matchAttempts);
    }
    catch (Exception ex)
    {
      Logger.Debug("Exception: {ex}", ex);
    }
  }

  private void OnFileMatched(object sender, FileEventArgs fileMatchedEvent)
  {
    var video = fileMatchedEvent.Video;
    var series = fileMatchedEvent.Series;
    var episodes = fileMatchedEvent.Episodes;

    if (episodes.Count == 0 && series.Count == 0)
    {
      foreach (var crossReference in video.CrossReferences)
        _anidbTrackedEpisodeIds.Add(crossReference.AnidbEpisodeID);
      return;
    }

    if (episodes.Count == 0 || series.Count == 0) // This is just a safeguard...
    {
      // I don't know in what circumstance this code will execute...
      Logger.Error("I don't think this should ever be seen, but if it is... Let @fearnlj01 know it can happen (please).");
      return;
    }

    AttemptMatchedUpdate(series[0], episodes[0], video);
  }

  #nullable enable
  private async void AttemptMatchedUpdate(ISeries series, IEpisode episode, IVideo video)
  {
    if (
      !_settings.Webhook.Enabled ||
      !_fileTracker.TryRemoveFile(video.ID) ||
      !_messageTracker.TryGetValue(video.ID, out var messageId)
      )
    {
      return;
    }

    try
    {
      var poster = await _shokoHelper.GetSeriesPoster(series);
      var imageStream = poster != null ? await _shokoHelper.GetImageStream(poster) : null;

      await _discordHelper.PatchWebhook(video, series, episode, imageStream, messageId);
      _messageTracker.TryRemoveMessage(video.ID);
    }
    catch (Exception ex)
    {
      Logger.Debug("Exception: {ex}", ex);
    }
  }
  #nullable disable

  private async void OnAVDumpEvent(object sender, AVDumpEventArgs dumpEvent)
  {
    if (dumpEvent.Type != AVDumpEventType.Success || !_settings.Webhook.Enabled)
      return;

    foreach (var videoId in dumpEvent.VideoIDs ?? Array.Empty<int>())
    {
      if (!_fileTracker.TryGetValue(videoId, out var video)) continue;

      var searchResult = await _shokoHelper.MatchTitle(video.EarliestKnownName);

      var restrictions = _settings.Webhook.Restrictions;
      if (!restrictions.PostIfTopMatchRestricted && searchResult.List.First().Restricted)
        return;
      if (!restrictions.ShowRestrictedTitles)
        searchResult.List.RemoveAll(x => x.Restricted);

      // Sort the list to prioritise anything currently airing.
      searchResult.List.Sort((a, b) =>
      {
        if (a.IsCurrentlyAiring && b.IsCurrentlyAiring)
        {
          // This next line should never happen. An air date is required for IsCurrentlyAiring to be true
          if (!a.AirDate.HasValue || !b.AirDate.HasValue) return 0;
          if (a.AirDate == b.AirDate) return 0;
          return a.AirDate > b.AirDate ? -1 : 1;
        }

        if (a.IsCurrentlyAiring) return -1;
        return b.IsCurrentlyAiring ? 1 : 0;
      });

      searchResult.List = searchResult.List.Take(3).ToList();
      var messageId = await _discordHelper.SendWebhook(video, searchResult);

      _ = _messageTracker.TryAddMessage(video.ID, messageId);
    }
  }

  private void OnEpisodeUpdatedEvent(object sender, EpisodeInfoUpdatedEventArgs episodeInfoUpdatedEvent)
  {
    if (episodeInfoUpdatedEvent.Reason is not (UpdateReason.Added or UpdateReason.Updated)) return;

    var episode = episodeInfoUpdatedEvent.EpisodeInfo;
    var series = episodeInfoUpdatedEvent.SeriesInfo;

    var anidbEpisodeIds = episode.CrossReferences.Select(reference => reference.AnidbEpisodeID).ToHashSet();

    if (episode.VideoList.Count == 0) return;

    var removedLog = anidbEpisodeIds.Select(eid => _anidbTrackedEpisodeIds.Remove(eid)).ToList();
    if (!removedLog.Any(removedBool => removedBool)) return;

    Logger.Debug("Attempting to update webhook by episode added/updated.");
    AttemptMatchedUpdate(series, episode, episode.VideoList[0]);
  }

  private async Task WaitForRescan(IVideo video, int matchAttempts = 1)
  {
    // The subtracted seconds are to avoid the logout timer firing in ShokoServer @ the five-minute interval
    var waitPeriod = TimeSpan.FromMinutes(matchAttempts * 5) - TimeSpan.FromSeconds(15);
    await Task.Delay(waitPeriod);

    if (!_fileTracker.Contains(video.ID)) return;
    _ = _shokoHelper.ScanFile(video, matchAttempts);
  }
}
