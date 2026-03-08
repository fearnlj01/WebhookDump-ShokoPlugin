using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

/**
 * This class exists for the sake of allowing the database to be init after server startup.
 */
public class CachedDataProxy : ICachedDataProxy
{
  private readonly TaskCompletionSource<ICachedData> _proxyTcs =
    new(TaskCreationOptions.RunContinuationsAsynchronously);

  public async Task SaveMessageStateAsync(int videoId, MinimalMessageState messageState)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .SaveMessageStateAsync(videoId, messageState).ConfigureAwait(false);
  }

  public async Task<MinimalMessageState?> GetMessageStateAsync(int videoId)
  {
    return await (await GetDatabaseAsync().ConfigureAwait(false))
      .GetMessageStateAsync(videoId).ConfigureAwait(false);
  }

  public async Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync()
  {
    return await (await GetDatabaseAsync().ConfigureAwait(false))
      .GetAllMessagesAsync().ConfigureAwait(false);
  }

  public async Task DeleteEntryAsync(int videoId)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .DeleteEntryAsync(videoId).ConfigureAwait(false);
  }

  public async Task DeleteEntriesAsync(IEnumerable<int> videoIds)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .DeleteEntriesAsync(videoIds).ConfigureAwait(false);
  }

  public async Task SaveTrackedFileAsync(int fileId)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .SaveTrackedFileAsync(fileId).ConfigureAwait(false);
  }

  public async Task<bool> IsFileTrackedAsync(int fileId)
  {
    return await (await GetDatabaseAsync().ConfigureAwait(false))
      .IsFileTrackedAsync(fileId).ConfigureAwait(false);
  }

  public async Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds)
  {
    return await (await GetDatabaseAsync().ConfigureAwait(false))
      .GetTrackedFileIdsAsync(fileIds).ConfigureAwait(false);
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .CleanupOldEntriesAsync(retentionPeriod).ConfigureAwait(false);
  }

  public bool TrySetDatabase(ICachedData inner)
  {
    return _proxyTcs.TrySetResult(inner);
  }

  public bool TrySetException(Exception ex)
  {
    return _proxyTcs.TrySetException(ex);
  }

  private Task<ICachedData> GetDatabaseAsync()
  {
    return _proxyTcs.Task;
  }
}
