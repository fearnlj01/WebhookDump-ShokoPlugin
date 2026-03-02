using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Persistence;

public class CachedData : ICachedData
{
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

  public async Task SaveMessageStateAsync(int videoId, MinimalMessageState state)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      INSERT OR REPLACE INTO Messages (VideoId, JsonData, Timestamp)
      VALUES ($videoId, $jsonData, $timestamp)
      """;
    command.Parameters.AddWithValue("$videoId", videoId);
    command.Parameters.AddWithValue("$jsonData", JsonSerializer.Serialize(state));
    command.Parameters.AddWithValue("$timestamp", state.EarliestKnownDate.ToString("O"));

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task<IReadOnlyList<(int videoId, MinimalMessageState messageState)>> GetAllMessagesAsync()
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT VideoId, JsonData FROM Messages";

    var messages = new List<(int, MinimalMessageState)>();

    var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
      var videoId = reader.GetInt32(0);
      var jsonData = reader.GetString(1);
      var messageState = JsonSerializer.Deserialize<MinimalMessageState>(jsonData);
      if (messageState is not null) messages.Add((videoId, messageState));
    }

    return messages;
  }

  public async Task<MinimalMessageState?> GetMessageStateAsync(int videoId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT JsonData FROM Messages WHERE VideoId = $videoId";
    command.Parameters.AddWithValue("$videoId", videoId);

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    if (!await reader.ReadAsync().ConfigureAwait(false)) return null;

    var jsonData = reader.GetString(0);
    return JsonSerializer.Deserialize<MinimalMessageState>(jsonData);
  }

  public async Task DeleteMessageStateAsync(int videoId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM Messages WHERE VideoId = $videoId";
    command.Parameters.AddWithValue("$videoId", videoId);

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task CleanupOldEntriesAsync(TimeSpan retentionPeriod)
  {
    var cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod).ToString("O");

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      DELETE FROM Messages WHERE Timestamp < $cutoff;
      DELETE FROM TrackedFiles WHERE Timestamp < $cutoff;
      """;
    command.Parameters.AddWithValue("$cutoff", cutoff);

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task SaveTrackedFilesAsync(int fileId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText =
      """
      INSERT OR REPLACE INTO TrackedFiles (FileId, Timestamp)
      VALUES ($fileId, $timestamp)
      """;
    command.Parameters.AddWithValue("$fileId", fileId);
    command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.ToString("O"));

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
  }

  public async Task<bool> IsFileTrackedAsync(int fileId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM TrackedFiles WHERE FileId = $fileId)";
    command.Parameters.AddWithValue("$fileId", fileId);

    var result = (long?)await command.ExecuteScalarAsync().ConfigureAwait(false);
    return result.GetValueOrDefault() != 0;
  }

  public async Task<IReadOnlySet<int>> GetTrackedFileIdsAsync(IEnumerable<int> fileIds)
  {
    var ids = fileIds.Where(id => id > 0).Distinct().ToArray();

    if (ids.Length == 0) return new HashSet<int>();

    const int chunkSize = 500;

    var trackedIds = new HashSet<int>();

    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    for (var offset = 0; offset < ids.Length; offset += chunkSize)
    {
      var chunk = ids.AsSpan(offset, Math.Min(chunkSize, ids.Length - offset));

      await using var command = connection.CreateCommand();

      var sb = new StringBuilder();
      sb.Append("SELECT FileId FROM TrackedFiles WHERE FileId IN (");

      for (var i = 0; i < chunk.Length; i++)
      {
        if (i != 0) sb.Append(", ");
        var paramName = $"$id{i}";
        sb.Append(paramName);
        command.Parameters.AddWithValue(paramName, chunk[i]);
      }

      sb.Append(");");
      command.CommandText = sb.ToString();

      await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
      while (await reader.ReadAsync().ConfigureAwait(false)) trackedIds.Add(reader.GetInt32(0));
    }

    return trackedIds;
  }

  public async Task DeleteTrackedFilesAsync(int fileId)
  {
    await using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync().ConfigureAwait(false);

    await using var command = connection.CreateCommand();
    command.CommandText = "DELETE FROM TrackedFiles WHERE FileId = $fileId";
    command.Parameters.AddWithValue("$fileId", fileId);

    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
      CREATE TABLE IF NOT EXISTS Messages (
          VideoId INTEGER PRIMARY KEY,
          JsonData TEXT NOT NULL,
          Timestamp TEXT NOT NULL
      );

      CREATE TABLE IF NOT EXISTS TrackedFiles (
          FileId INTEGER PRIMARY KEY,
          Timestamp TEXT NOT NULL
      );

      CREATE INDEX IF NOT EXISTS IX_Messages_Timestamp ON Messages(Timestamp);
      CREATE INDEX IF NOT EXISTS IX_TrackedFiles_Timestamp ON TrackedFiles(Timestamp);
      """;
    command.ExecuteNonQuery();
  }
}
