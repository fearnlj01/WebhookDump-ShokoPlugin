namespace Shoko.Plugin.WebhookDump.Discord.Models;

public record MinimalMessageState
{
  public required string Id { get; set; }
  public IReadOnlyList<Reaction> Reactions { get; init; } = [];
  public DateTimeOffset EarliestKnownDate { get; init; } = DateTimeOffset.UtcNow;
}
