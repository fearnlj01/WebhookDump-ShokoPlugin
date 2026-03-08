using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

public interface ICachedData
{
  Task SaveMessageStateAsync(int videoId, MinimalMessageState messageState);
  Task<MinimalMessageState?> GetMessageStateAsync(int videoId);
  Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync();
  Task DeleteEntryAsync(int videoId);
  Task DeleteEntriesAsync(IEnumerable<int> videoIds);

  Task SaveTrackedFileAsync(int fileId);
  Task<bool> IsFileTrackedAsync(int fileId);
  Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds);

  Task CleanupOldEntriesAsync(TimeSpan retentionPeriod);
}
