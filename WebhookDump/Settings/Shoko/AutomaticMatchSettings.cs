namespace Shoko.Plugin.WebhookDump.Settings.Shoko;

public class AutomaticMatchSettings
{
  public bool Enabled { get; set; } = true;
  public bool WatchReactions { get; set; }
  public int MaxAttempts { get; set; } = 8;
}
