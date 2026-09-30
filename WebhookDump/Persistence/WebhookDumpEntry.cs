namespace Shoko.Plugin.WebhookDump.Persistence;

public class WebhookDumpEntry
{
  public int VideoFileId { get; set; }
  public long? DiscordMessageId { get; set; }
  public DateTime InsertionTimestamp { get; set; }
}
