namespace Shoko.Plugin.WebhookDump.Models;

public interface IWithDate
{
  public DateTimeOffset EarliestKnownDate { get; init; }
}
