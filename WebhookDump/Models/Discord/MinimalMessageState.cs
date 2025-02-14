namespace Shoko.Plugin.WebhookDump.Models.Discord;

public record MinimalMessageState : IWithDate
{
  public required string Id { get; set; }
  public IReadOnlyList<Reaction> Reactions { get; init; } = [];
  public DateTimeOffset EarliestKnownDate { get; init; } = DateTimeOffset.UtcNow;
}
