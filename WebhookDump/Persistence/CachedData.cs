using Microsoft.Data.Sqlite;
using Shoko.Plugin.WebhookDump.Discord.Models;
using Shoko.Plugin.WebhookDump.Extensions;

namespace Shoko.Plugin.WebhookDump.Persistence;

public class CachedData : ICachedData
{
  private const int MaxChunkSize = 500;
  private readonly string _connectionString;

  public CachedData(string dbPath)
  {
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

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
    if (!await reader.ReadAsync().ConfigureAwait(false)) return null;
    if (reader.IsDBNull(0)) return null;

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

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task<bool> IsFileTrackedAsync(int fileId)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileId);

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM WebhookDumpEntries WHERE VideoFileId = $fileId)";
    command.Parameters.AddWithValue("$fileId", fileId);

    var result = (long?)await command.ExecuteScalarAsync().ConfigureAwait(false);
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
      while (await reader.ReadAsync().ConfigureAwait(false)) trackedIds.Add(reader.GetInt32(0));
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

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    var cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToString("O");

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM WebhookDumpEntries WHERE InsertionTimestamp < $cutoff;";
    command.Parameters.AddWithValue("$cutoff", cutoff);

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

      await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
    command.ExecuteNonQuery();
  }
}
