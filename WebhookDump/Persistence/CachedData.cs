using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.WebhookDump.Discord.Models;
using Shoko.Plugin.WebhookDump.Extensions;

namespace Shoko.Plugin.WebhookDump.Persistence;

public partial class CachedData : ICachedData
{
  private const int MaxChunkSize = 500;
  private readonly string _connectionString;
  private readonly ILogger<CachedData> _logger;

  public CachedData(string dbPath, ILogger<CachedData> logger)
  {
    _logger = logger;
    _connectionString = new SqliteConnectionStringBuilder
    {
      DataSource = dbPath,
      Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    InitializeDatabase();
  }

  public async Task SaveMessageStateAsync(int videoId, MinimalMessageState messageState)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(videoId);

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      INSERT INTO WebhookDumpEntries (VideoFileId, DiscordMessageId, InsertionTimestamp)
      VALUES ($videoId, $discordMessageId, $timestamp)
      ON CONFLICT(VideoFileId) DO UPDATE SET
        DiscordMessageId = excluded.DiscordMessageId;
      """;
    command.Parameters.AddWithValue("$videoId", videoId);
    command.Parameters.AddWithValue("$discordMessageId", messageState.Id);
    command.Parameters.AddWithValue("$timestamp", messageState.EarliestKnownDate.ToString("O"));

    try
    {
      if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
        LogUpdatedMessageState(_logger, videoId, messageState.Id);
    }
    catch (DbException ex)
    {
      LogFailedToUpdatedMessageState(_logger, ex, videoId, messageState.Id);
    }
  }

  public async Task<MinimalMessageState?> GetMessageStateAsync(int videoId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(videoId);

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      "SELECT DiscordMessageId, InsertionTimestamp FROM WebhookDumpEntries WHERE VideoFileId = $videoId;";
    command.Parameters.AddWithValue("$videoId", videoId);

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    try
    {
      if (!await reader.ReadAsync().ConfigureAwait(false)) return null;
      if (await reader.IsDBNullAsync(0).ConfigureAwait(false)) return null;

      var messageIdSigned = reader.GetInt64(0);
      if (messageIdSigned <= 0) return null;

      _ = reader.TryGetDateTimeOffset(1, out var insertionTimestamp);

      return new MinimalMessageState
      {
        Id = (ulong)messageIdSigned,
        EarliestKnownDate = insertionTimestamp,
        Reactions = []
      };
    }
    catch (DbException ex)
    {
      LogFailedToRetrieveMessageState(_logger, ex, videoId);
      return null;
    }
  }

  public async Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync()
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      SELECT VideoFileId, DiscordMessageId, InsertionTimestamp
      FROM WebhookDumpEntries
      WHERE DiscordMessageId IS NOT NULL;
      """;

    var messages = new List<(int, MinimalMessageState)>();

    var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    try
    {
      while (await reader.ReadAsync().ConfigureAwait(false))
      {
        var videoId = reader.GetInt32(0);

        var messageIdSigned = reader.GetInt64(1);
        if (messageIdSigned <= 0) continue;

        _ = reader.TryGetDateTimeOffset(2, out var insertionTimestamp);

        messages.Add((videoId, new MinimalMessageState
        {
          Id = (ulong)messageIdSigned,
          EarliestKnownDate = insertionTimestamp,
          Reactions = []
        }));
      }
    }
    catch (DbException ex)
    {
      LogFailedToRetrieveMessages(_logger, ex);
    }

    return messages;
  }

  public async Task SaveTrackedFileAsync(int fileId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileId);

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      INSERT OR IGNORE INTO WebhookDumpEntries (VideoFileId, DiscordMessageId, InsertionTimestamp)
      VALUES ($fileId, NULL, $timestamp)
      """;
    command.Parameters.AddWithValue("$fileId", fileId);
    command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O"));

    try
    {
      if (await command.ExecuteNonQueryAsync().ConfigureAwait(false) > 0)
        LogTrackedFile(_logger, fileId);
    }
    catch (DbException ex)
    {
      LogFailedToTrackFile(_logger, ex, fileId);
    }
  }

  public async Task<bool> IsFileTrackedAsync(int fileId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileId);

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM WebhookDumpEntries WHERE VideoFileId = $fileId)";
    command.Parameters.AddWithValue("$fileId", fileId);

    long? result = null;
    try
    {
      result = (long?)await command.ExecuteScalarAsync().ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToCheckFileTracked(_logger, ex, fileId);
    }

    return result.GetValueOrDefault() != 0;
  }

  public async Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds)
  {
    var ids = fileIds.Where(id => id > 0).Distinct().ToArray();
    if (ids.Length == 0) return new HashSet<int>();

    var trackedIds = new HashSet<int>();

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    for (var offset = 0; offset < ids.Length; offset += MaxChunkSize)
    {
      var chunk = ids.AsSpan(offset, Math.Min(MaxChunkSize, ids.Length - offset));

      await using var command = connection.CreateCommand();

      var parameterList = command.AddIntParameterList("$id", chunk);
      command.CommandText = $"SELECT VideoFileId FROM WebhookDumpEntries WHERE VideoFileId IN ({parameterList})";

      await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
      try
      {
        while (await reader.ReadAsync().ConfigureAwait(false))
          trackedIds.Add(reader.GetInt32(0));
      }
      catch (DbException ex)
      {
        LogFailedToRetrieveTrackedFileIds(_logger, ex);
      }
    }

    return trackedIds;
  }

  public async Task DeleteEntryAsync(int videoId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM WebhookDumpEntries WHERE VideoFileId = $videoId";
    command.Parameters.AddWithValue("$videoId", videoId);

    try
    {
      await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToDeleteEntry(_logger, ex, videoId);
    }
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    var cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToString("O");

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM WebhookDumpEntries WHERE InsertionTimestamp < $cutoff;";
    command.Parameters.AddWithValue("$cutoff", cutoff);

    try
    {
      await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
    catch (DbException ex)
    {
      LogFailedToCleanupOldEntries(_logger, ex);
    }
  }

  public async Task DeleteEntriesAsync(IEnumerable<int> videoIds)
  {
    var ids = videoIds.Distinct().ToArray();
    if (ids.Length == 0) return;

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    for (var offset = 0; offset < ids.Length; offset += MaxChunkSize)
    {
      var chunk = ids.AsSpan(offset, Math.Min(MaxChunkSize, ids.Length - offset));

      await using var command = connection.CreateCommand();

      var parameterList = command.AddIntParameterList("$id", chunk);
      command.CommandText = $"DELETE FROM WebhookDumpEntries WHERE VideoFileId IN ({parameterList})";

      try
      {
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
      }
      catch (DbException ex)
      {
        LogFailedToDeleteEntries(_logger, ex);
      }
    }
  }

  private void InitializeDatabase()
  {
    using var connection = new SqliteConnection(_connectionString);
    connection.Open();

    using (var walCommand = connection.CreateCommand())
    {
      walCommand.CommandText = "PRAGMA journal_mode=WAL;";
      walCommand.ExecuteNonQuery();
    }

    using var command = connection.CreateCommand();
    command.CommandText =
      """
      CREATE TABLE IF NOT EXISTS WebhookDumpEntries (
        VideoFileId INTEGER PRIMARY KEY,
        DiscordMessageId INTEGER NULL,
        InsertionTimestamp TEXT NOT NULL
      );

      CREATE INDEX IF NOT EXISTS IX_WebhookDumpEntries_InsertionTimestamp ON WebhookDumpEntries(InsertionTimestamp);
      """;
    if (command.ExecuteNonQuery() > 0) LogDatabaseInit(_logger);
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

  [LoggerMessage(LogLevel.Information, "Plugin database initialized successfully.")]
  static partial void LogDatabaseInit(ILogger<CachedData> logger);

  #endregion
}
