namespace Shoko.Plugin.WebhookDump.Models.Shoko.Common;

public class Rating
{
  public double Value { get; init; }
  public int MaxValue { get; init; }
  public string Source { get; init; } = string.Empty;
  public int Votes { get; init; }
  public string? Type { get; init; }
}
