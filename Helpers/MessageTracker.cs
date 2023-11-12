using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Apis;

public class MessageTracker : IMessageTracker, IDisposable
{
  private readonly Timer _timer;
  private readonly ISettingsProvider _settingsProvider;
  private readonly ISettings _settings;
  private readonly IDiscordHelper _discordHelper;
  private readonly IShokoHelper _shokoHelper;
  private readonly Dictionary<int, string> _messageSet;
  private readonly HashSet<int> _processedSet;
  private readonly Logger _logger = LogManager.GetCurrentClassLogger();

  public MessageTracker(ISettingsProvider settingsProvider, IDiscordHelper discordHelper, IShokoHelper shokoHelper)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    _discordHelper = discordHelper;
    _shokoHelper = shokoHelper;

    _messageSet = new();
    _processedSet = new();

    if (_settings.Shoko.AutomaticMatch.WatchReactions)
    {
      _timer = new()
      {
        AutoReset = true,
        Enabled = true,
        Interval = TimeSpan.FromMinutes(15).TotalMilliseconds,
      };
      _timer.Elapsed += CheckAllMessages;
    }
  }

  public bool TryAddMessage(int fileId, string messageId)
  {
    return _messageSet.TryAdd(fileId, messageId);
  }

  public bool TryRemoveMessage(int fileId)
  {
    return _messageSet.Remove(fileId);
  }

  public bool Contains(int fileId)
  {
    return _messageSet.ContainsKey(fileId);
  }

  public bool TryGetValue(int fileId, out string messageId)
  {
    return _messageSet.TryGetValue(fileId, out messageId);
  }

  private async void CheckAllMessages(object sender, ElapsedEventArgs e)
  {
    foreach (KeyValuePair<int, string> message in _messageSet)
    {
      if (_processedSet.Contains(message.Key))
      {
        continue;
      }

      if (await _discordHelper.GetMessageReactionState(message.Value))
      {
        _logger.Info($"Triggering rescan for (fileId={message.Key}, messageId={message.Value})");
        _ = Task.Run(() => _shokoHelper.ScanFileById(message.Key)).ConfigureAwait(false);
        _ = _processedSet.Add(message.Key);
      }
    }
  }

  public void Dispose()
  {
    _timer.Dispose();
    _discordHelper.Dispose();
    _shokoHelper.Dispose();
    GC.SuppressFinalize(this);
  }
}
