namespace Shoko.Plugin.WebhookDump.Settings;

public interface IAutomaticMatchSettings
{
  bool Enabled { get; set; }
  bool WatchReactions { get; set; }
  int MaxAttempts { get; set; }
}