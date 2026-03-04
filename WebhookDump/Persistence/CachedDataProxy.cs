using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

/**
 * This class exists for the sake of allowing the database to be init after server startup.
 */
public class CachedDataProxy : ICachedDataProxy
{
  private readonly TaskCompletionSource<ICachedData> _proxyTaskCompletionSource =
    new(TaskCreationOptions.RunContinuationsAsynchronously);

  public async Task SaveMessageStateAsync(int videoId, MinimalMessageState state)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .SaveMessageStateAsync(videoId, state).ConfigureAwait(false);
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

  public async Task DeleteMessageStateAsync(int videoId)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .DeleteMessageStateAsync(videoId).ConfigureAwait(false);
  }

  public async Task SaveTrackedFilesAsync(int fileId)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .SaveTrackedFilesAsync(fileId).ConfigureAwait(false);
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

  public async Task DeleteTrackedFilesAsync(int fileId)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .DeleteTrackedFilesAsync(fileId).ConfigureAwait(false);
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    await (await GetDatabaseAsync().ConfigureAwait(false))
      .CleanupOldEntriesAsync(retentionPeriod).ConfigureAwait(false);
  }

  public bool TrySetDatabase(ICachedData inner)
  {
    return _proxyTaskCompletionSource.TrySetResult(inner);
  }

  public bool TrySetException(Exception ex)
  {
    return _proxyTaskCompletionSource.TrySetException(ex);
  }

  private Task<ICachedData> GetDatabaseAsync()
  {
    return _proxyTaskCompletionSource.Task;
  }
}
