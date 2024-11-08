using System.Timers;
using NLog;
using Shoko.Plugin.WebhookDump.Settings;
using Timer = System.Timers.Timer;

namespace Shoko.Plugin.WebhookDump.Apis;

public sealed class MessageTracker : IDisposable
{
  private readonly Timer _timer;
  private readonly DiscordHelper _discordHelper;
  private readonly ShokoHelper _shokoHelper;
  private readonly Dictionary<int, string> _messageSet;
  private readonly HashSet<int> _processedSet;
  private readonly Logger _logger = LogManager.GetCurrentClassLogger();

  public MessageTracker(SettingsProvider settingsProvider, DiscordHelper discordHelper, ShokoHelper shokoHelper)
  {
    var settings = settingsProvider.GetSettings();

    _discordHelper = discordHelper;
    _shokoHelper = shokoHelper;

    _messageSet = new Dictionary<int, string>();
    _processedSet = [];

    if (!settings.Shoko.AutomaticMatch.WatchReactions) return;
    _timer = new Timer
    {
      AutoReset = true,
      Enabled = true,
      Interval = TimeSpan.FromMinutes(15).TotalMilliseconds,
    };
    _timer.Elapsed += CheckAllMessages;
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
    foreach (var message in _messageSet.Where(message => !_processedSet.Contains(message.Key)))
    {
      if (!await _discordHelper.GetMessageReactionState(message.Value)) continue;
      _logger.Info($"Triggering rescan for (fileId={message.Key}, messageId={message.Value})");
      _ = Task.Run(() => _shokoHelper.ScanFileById(message.Key)).ConfigureAwait(false);
      _ = _processedSet.Add(message.Key);
    }
  }

  #region Disposal

  private bool _disposed;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  private void Dispose(bool isDisposing)
  {
    if (_disposed) return;
    if (isDisposing)
    {
      _timer.Dispose();
      _discordHelper.Dispose();
      _shokoHelper.Dispose();
    }
    _disposed = true;
  }

  ~MessageTracker()
  {
    Dispose(false);
  }
  #endregion Disposal
}
