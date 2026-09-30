using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Tests;

public class LegacyDatabaseImporterTests
{
  [Fact]
  public async Task Import_CopiesEveryEntryOnceAndRenamesTheFile()
  {
    await using var database = await TestDatabase.CreateAsync();
    var legacyPath = Path.Combine(database.Folder, "WebhookDump.db");
    await CreateLegacyDatabaseAsync(legacyPath,
      (1, 1_300_000_000_000_000_000L, "2026-09-01T10:00:00.0000000+00:00"),
      (2, null, "2026-09-02T12:30:00.0000000+02:00"),
      (3, 30L, "not a date"),
      (4, 40L, "2026-09-03T08:15:00.123456"));

    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);

    Assert.False(File.Exists(legacyPath));
    Assert.True(File.Exists(legacyPath + ".migrated"));
    Assert.False(File.Exists(legacyPath + "-wal"));
    Assert.False(File.Exists(legacyPath + ".migrated-wal"));

    await using var context = database.CreateDbContext();
    var entries = await context.Entries.OrderBy(e => e.VideoFileId).ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal([1, 2, 3, 4], entries.Select(e => e.VideoFileId));
    Assert.Equal(1_300_000_000_000_000_000L, entries[0].DiscordMessageId);
    Assert.Equal(new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc), entries[0].InsertionTimestamp);
    Assert.Null(entries[1].DiscordMessageId);
    Assert.Equal(new DateTime(2026, 9, 2, 10, 30, 0, DateTimeKind.Utc), entries[1].InsertionTimestamp);
    Assert.Equal(DateTimeKind.Utc, entries[1].InsertionTimestamp.Kind);
    Assert.InRange(entries[2].InsertionTimestamp, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    Assert.Equal(new DateTime(2026, 9, 3, 8, 15, 0, 123, 456, DateTimeKind.Utc), entries[3].InsertionTimestamp);

    // The next start finds nothing to carry over.
    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);
    Assert.Equal(4, await context.Entries.CountAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Import_SkipsTheCopyWhenTheNewDatabaseHasEntries()
  {
    await using var database = await TestDatabase.CreateAsync();
    var legacyPath = Path.Combine(database.Folder, "WebhookDump.db");
    await CreateLegacyDatabaseAsync(legacyPath, (1, 10L, "2026-09-01T10:00:00.0000000+00:00"));
    await using (var context = database.CreateDbContext())
    {
      context.Entries.Add(new WebhookDumpEntry { VideoFileId = 9, InsertionTimestamp = DateTime.UtcNow });
      await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);

    Assert.True(File.Exists(legacyPath + ".migrated"));
    await using (var context = database.CreateDbContext())
      Assert.Equal([9], await context.Entries.Select(e => e.VideoFileId).ToListAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Import_KeepsAnEarlierMigratedFile()
  {
    await using var database = await TestDatabase.CreateAsync();
    var legacyPath = Path.Combine(database.Folder, "WebhookDump.db");
    await File.WriteAllTextAsync(legacyPath + ".migrated", "earlier", TestContext.Current.CancellationToken);
    await CreateLegacyDatabaseAsync(legacyPath, (1, 10L, "2026-09-01T10:00:00.0000000+00:00"));

    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);

    Assert.False(File.Exists(legacyPath));
    Assert.Equal("earlier", await File.ReadAllTextAsync(legacyPath + ".migrated", TestContext.Current.CancellationToken));
    Assert.Single(Directory.GetFiles(database.Folder, "WebhookDump.db.*.migrated"));
  }

  [Fact]
  public async Task Import_MovesAnUnreadableFileAside()
  {
    await using var database = await TestDatabase.CreateAsync();
    var legacyPath = Path.Combine(database.Folder, "WebhookDump.db");
    var contents = string.Concat(Enumerable.Repeat("this is not a SQLite database. ", 64));
    await File.WriteAllTextAsync(legacyPath, contents, TestContext.Current.CancellationToken);

    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);

    Assert.False(File.Exists(legacyPath));
    Assert.False(File.Exists(legacyPath + ".migrated"));
    Assert.Equal(contents, await File.ReadAllTextAsync(legacyPath + ".unreadable", TestContext.Current.CancellationToken));
    await using var context = database.CreateDbContext();
    Assert.False(await context.Entries.AnyAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Import_DoesNothingWithoutTheOldFile()
  {
    await using var database = await TestDatabase.CreateAsync();
    var legacyPath = Path.Combine(database.Folder, "WebhookDump.db");

    await CreateImporter(database).ImportAsync(legacyPath, TestContext.Current.CancellationToken);

    Assert.False(File.Exists(legacyPath));
    Assert.False(File.Exists(legacyPath + ".migrated"));
  }

  private static LegacyDatabaseImporter CreateImporter(TestDatabase database)
  {
    return new LegacyDatabaseImporter(database, NullLogger<LegacyDatabaseImporter>.Instance);
  }

  // Creates the file the way the released plugin did, in WAL mode, and copies it while it is still open, as a server
  // that was killed would leave it: the rows are only in the "-wal" file next to it.
  private static async Task CreateLegacyDatabaseAsync(string path,
    params (int VideoFileId, long? DiscordMessageId, string InsertionTimestamp)[] rows)
  {
    var sourcePath = path + ".source";
    await using var connection = new SqliteConnection($"Data Source={sourcePath};Pooling=False");
    await connection.OpenAsync(TestContext.Current.CancellationToken);

    await using (var command = connection.CreateCommand())
    {
      command.CommandText =
        """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS WebhookDumpEntries (
          VideoFileId INTEGER PRIMARY KEY,
          DiscordMessageId INTEGER NULL,
          InsertionTimestamp TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WebhookDumpEntries_InsertionTimestamp ON WebhookDumpEntries(InsertionTimestamp);
        """;
      await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    foreach (var (videoFileId, discordMessageId, insertionTimestamp) in rows)
    {
      await using var command = connection.CreateCommand();
      command.CommandText =
        "INSERT INTO WebhookDumpEntries (VideoFileId, DiscordMessageId, InsertionTimestamp) VALUES ($id, $message, $timestamp)";
      command.Parameters.AddWithValue("$id", videoFileId);
      command.Parameters.AddWithValue("$message", (object?)discordMessageId ?? DBNull.Value);
      command.Parameters.AddWithValue("$timestamp", insertionTimestamp);
      await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    File.Copy(sourcePath, path);
    File.Copy(sourcePath + "-wal", path + "-wal");
  }
}
