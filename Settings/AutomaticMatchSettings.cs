namespace Shoko.Plugin.WebhookDump.Settings;

public class AutomaticMatchSettings : IAutomaticMatchSettings
{
  public bool Enabled { get; set; }
  public bool WatchReactions { get; set; }
  public int MaxAttempts { get; set; }
}
