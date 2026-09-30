using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

public sealed partial class CachedData(IDbContextFactory<WebhookDumpDbContext> contextFactory, ILogger<CachedData> logger)
  : ICachedData, IDisposable
{
  private const int MaxChunkSize = 500;

  // Serializes the insert-or-update writes, so two of them for the same video can't both try to insert it.
  private readonly SemaphoreSlim _writeLock = new(1, 1);

  public async Task SaveMessageStateAsync(int videoId, MinimalMessageState messageState)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(videoId);
    ArgumentNullException.ThrowIfNull(messageState);

    await _writeLock.WaitAsync().ConfigureAwait(false);
    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      var entry = await context.Entries.FindAsync(videoId).ConfigureAwait(false);
      if (entry is null)
        context.Entries.Add(new WebhookDumpEntry
        {
          VideoFileId = videoId,
          DiscordMessageId = (long)messageState.Id,
          InsertionTimestamp = messageState.EarliestKnownDate.UtcDateTime
        });
      else
        entry.DiscordMessageId = (long)messageState.Id;

      if (await context.SaveChangesAsync().ConfigureAwait(false) > 0)
        LogUpdatedMessageState(logger, videoId, messageState.Id);
    }
    catch (Exception ex) when (ex is DbException or DbUpdateException)
    {
      LogFailedToUpdatedMessageState(logger, ex, videoId, messageState.Id);
    }
    finally
    {
      _writeLock.Release();
    }
  }

  public async Task<MinimalMessageState?> GetMessageStateAsync(int videoId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(videoId);

    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      var entry = await context.Entries
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.VideoFileId == videoId)
        .ConfigureAwait(false);

      return entry is { DiscordMessageId: > 0 } ? ToMessageState(entry) : null;
    }
    catch (DbException ex)
    {
      LogFailedToRetrieveMessageState(logger, ex, videoId);
      return null;
    }
  }

  public async Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync()
  {
    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      var entries = await context.Entries
        .AsNoTracking()
        .Where(e => e.DiscordMessageId > 0)
        .ToListAsync()
        .ConfigureAwait(false);

      return entries.Select(e => (e.VideoFileId, ToMessageState(e))).ToList();
    }
    catch (DbException ex)
    {
      LogFailedToRetrieveMessages(logger, ex);
      return [];
    }
  }

  public async Task SaveTrackedFileAsync(int fileId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileId);

    await _writeLock.WaitAsync().ConfigureAwait(false);
    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      if (await context.Entries.AnyAsync(e => e.VideoFileId == fileId).ConfigureAwait(false))
        return;

      context.Entries.Add(new WebhookDumpEntry
      {
        VideoFileId = fileId,
        DiscordMessageId = null,
        InsertionTimestamp = DateTime.UtcNow
      });

      if (await context.SaveChangesAsync().ConfigureAwait(false) > 0)
        LogTrackedFile(logger, fileId);
    }
    catch (Exception ex) when (ex is DbException or DbUpdateException)
    {
      LogFailedToTrackFile(logger, ex, fileId);
    }
    finally
    {
      _writeLock.Release();
    }
  }

  public async Task<bool> IsFileTrackedAsync(int fileId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileId);

    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      return await context.Entries.AnyAsync(e => e.VideoFileId == fileId).ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToCheckFileTracked(logger, ex, fileId);
      return false;
    }
  }

  public async Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds)
  {
    var ids = fileIds.Where(id => id > 0).Distinct().ToArray();
    if (ids.Length == 0) return new HashSet<int>();

    var trackedIds = new HashSet<int>();

    await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

    foreach (var chunk in ids.Chunk(MaxChunkSize))
    {
      try
      {
        var found = await context.Entries
          .Where(e => chunk.Contains(e.VideoFileId))
          .Select(e => e.VideoFileId)
          .ToListAsync()
          .ConfigureAwait(false);

        trackedIds.UnionWith(found);
      }
      catch (DbException ex)
      {
        LogFailedToRetrieveTrackedFileIds(logger, ex);
      }
    }

    return trackedIds;
  }

  public async Task DeleteEntryAsync(int videoId)
  {
    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      await context.Entries.Where(e => e.VideoFileId == videoId).ExecuteDeleteAsync().ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToDeleteEntry(logger, ex, videoId);
    }
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    var cutoff = DateTime.UtcNow.Subtract(retentionPeriod);

    try
    {
      await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

      await context.Entries.Where(e => e.InsertionTimestamp < cutoff).ExecuteDeleteAsync().ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToCleanupOldEntries(logger, ex);
    }
  }

  public async Task DeleteEntriesAsync(IEnumerable<int> videoIds)
  {
    var ids = videoIds.Distinct().ToArray();
    if (ids.Length == 0) return;

    await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);

    foreach (var chunk in ids.Chunk(MaxChunkSize))
    {
      try
      {
        await context.Entries.Where(e => chunk.Contains(e.VideoFileId)).ExecuteDeleteAsync().ConfigureAwait(false);
      }
      catch (DbException ex)
      {
        LogFailedToDeleteEntries(logger, ex);
      }
    }
  }

  public void Dispose()
  {
    _writeLock.Dispose();
  }

  private static MinimalMessageState ToMessageState(WebhookDumpEntry entry)
  {
    return new MinimalMessageState
    {
      Id = (ulong)entry.DiscordMessageId.GetValueOrDefault(),
      EarliestKnownDate = new DateTimeOffset(entry.InsertionTimestamp, TimeSpan.Zero),
      Reactions = []
    };
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Trace, "Updated message state for video (VideoId={VideoId},MessageId={MessageId})")]
  static partial void LogUpdatedMessageState(ILogger<CachedData> logger, int videoId, ulong messageId);

  [LoggerMessage(LogLevel.Error, "Failed to updated message state for video (VideoId={videoId},MessageId={messageId})")]
  static partial void LogFailedToUpdatedMessageState(ILogger<CachedData> logger, Exception ex, int videoId,
    ulong messageId);

  [LoggerMessage(LogLevel.Error, "Failed to retrieve message state for video (VideoId={VideoId})")]
  static partial void LogFailedToRetrieveMessageState(ILogger<CachedData> logger, Exception ex, int videoId);

  [LoggerMessage(LogLevel.Error, "Failed to retrieve all messages")]
  static partial void LogFailedToRetrieveMessages(ILogger<CachedData> logger, Exception ex);

  [LoggerMessage(LogLevel.Trace, "Tracked file (VideoId={videoId})")]
  static partial void LogTrackedFile(ILogger<CachedData> logger, int videoId);

  [LoggerMessage(LogLevel.Error, "Failed to track file (VideoId={VideoId})")]
  static partial void LogFailedToTrackFile(ILogger<CachedData> logger, Exception ex, int videoId);

  [LoggerMessage(LogLevel.Error, "Failed to check if file is tracked (VideoId={VideoId})")]
  static partial void LogFailedToCheckFileTracked(ILogger<CachedData> logger, Exception ex, int videoId);

  [LoggerMessage(LogLevel.Error, "Failed to retrieve tracked file ids")]
  static partial void LogFailedToRetrieveTrackedFileIds(ILogger<CachedData> logger, Exception ex);

  [LoggerMessage(LogLevel.Error, "Failed to delete entry for video (VideoId={VideoId})")]
  static partial void LogFailedToDeleteEntry(ILogger<CachedData> logger, Exception ex, int videoId);

  [LoggerMessage(LogLevel.Error, "Failed to cleanup old entries")]
  static partial void LogFailedToCleanupOldEntries(ILogger<CachedData> logger, Exception ex);

  [LoggerMessage(LogLevel.Error, "Failed to delete entries for videos")]
  static partial void LogFailedToDeleteEntries(ILogger<CachedData> logger, Exception ex);

  #endregion
}
