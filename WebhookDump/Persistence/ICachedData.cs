using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

public interface ICachedData
{
  Task SaveMessageStateAsync(int videoId, MinimalMessageState state);
  Task<MinimalMessageState?> GetMessageStateAsync(int videoId);
  Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync();
  Task DeleteMessageStateAsync(int videoId);

  Task SaveTrackedFilesAsync(int fileId);
  Task<bool> IsFileTrackedAsync(int fileId);
  Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds);
  Task DeleteTrackedFilesAsync(int fileId);

  Task CleanupOldEntriesAsync(TimeSpan retentionPeriod);
}
