using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Plugin.WebhookDump.Discord.Models;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Tests;

public class CachedDataTests
{
  [Fact]
  public async Task SaveMessageState_InsertsAndThenOnlyUpdatesTheMessageId()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);
    var firstSeen = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.FromHours(2));

    await cachedData.SaveMessageStateAsync(7, new MinimalMessageState { Id = 100, EarliestKnownDate = firstSeen });
    await cachedData.SaveMessageStateAsync(7, new MinimalMessageState { Id = 200, EarliestKnownDate = DateTimeOffset.UtcNow });

    var state = await cachedData.GetMessageStateAsync(7);
    Assert.NotNull(state);
    Assert.Equal(200UL, state.Id);
    Assert.Equal(firstSeen, state.EarliestKnownDate);
  }

  [Fact]
  public async Task MessageIdsAboveInt64MaxValueAreNotCorrupted()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);

    // Discord snowflakes fit in a signed 64-bit integer, as the old database stored them.
    const ulong snowflake = 1_300_000_000_000_000_000UL;
    await cachedData.SaveMessageStateAsync(1, new MinimalMessageState { Id = snowflake });

    Assert.Equal(snowflake, (await cachedData.GetMessageStateAsync(1))?.Id);
  }

  [Fact]
  public async Task TrackedFiles_AreFoundWithoutAMessageAndKeepTheirMessage()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);

    await cachedData.SaveTrackedFileAsync(1);
    await cachedData.SaveMessageStateAsync(2, new MinimalMessageState { Id = 20 });
    await cachedData.SaveTrackedFileAsync(2);

    Assert.True(await cachedData.IsFileTrackedAsync(1));
    Assert.True(await cachedData.IsFileTrackedAsync(2));
    Assert.False(await cachedData.IsFileTrackedAsync(3));
    Assert.Null(await cachedData.GetMessageStateAsync(1));
    Assert.Equal(20UL, (await cachedData.GetMessageStateAsync(2))?.Id);

    var messages = await cachedData.GetAllMessagesAsync();
    var (videoId, messageState) = Assert.Single(messages);
    Assert.Equal(2, videoId);
    Assert.Equal(20UL, messageState.Id);
  }

  [Fact]
  public async Task GetTrackedFileIds_ReadsEveryChunk()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);

    foreach (var id in Enumerable.Range(1, 1200).Where(id => id % 3 == 0))
      await cachedData.SaveTrackedFileAsync(id);

    var tracked = await cachedData.GetTrackedFileIdsAsync(Enumerable.Range(-5, 1300));

    Assert.Equal(Enumerable.Range(1, 1200).Where(id => id % 3 == 0).ToHashSet(), tracked);
  }

  [Fact]
  public async Task Deletes_RemoveOnlyTheGivenEntries()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);

    foreach (var id in Enumerable.Range(1, 1100))
      await cachedData.SaveTrackedFileAsync(id);

    await cachedData.DeleteEntryAsync(1);
    await cachedData.DeleteEntriesAsync(Enumerable.Range(2, 1000));

    var tracked = await cachedData.GetTrackedFileIdsAsync(Enumerable.Range(1, 1100));
    Assert.Equal(Enumerable.Range(1002, 99).ToHashSet(), tracked);
  }

  [Fact]
  public async Task CleanupOldEntries_RemovesOnlyEntriesPastTheRetentionPeriod()
  {
    await using var database = await TestDatabase.CreateAsync();
    using var cachedData = new CachedData(database, NullLogger<CachedData>.Instance);

    await cachedData.SaveMessageStateAsync(1,
      new MinimalMessageState { Id = 10, EarliestKnownDate = DateTimeOffset.UtcNow.AddDays(-15) });
    await cachedData.SaveMessageStateAsync(2,
      new MinimalMessageState { Id = 20, EarliestKnownDate = DateTimeOffset.UtcNow.AddDays(-13) });
    await cachedData.SaveTrackedFileAsync(3);

    await cachedData.CleanupOldEntriesAsync(TimeSpan.FromDays(14));

    Assert.False(await cachedData.IsFileTrackedAsync(1));
    Assert.True(await cachedData.IsFileTrackedAsync(2));
    Assert.True(await cachedData.IsFileTrackedAsync(3));
  }
}
