using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shoko.Plugin.WebhookDump.Persistence;

/**
 * Carries the entries of the SQLite file the plugin kept its data in before it moved to the server's plugin database
 * over into the new database, once, and then renames the old file to "*.migrated", or to "*.unreadable" when it is
 * not a SQLite database or is corrupt.
 */
public partial class LegacyDatabaseImporter(
  IDbContextFactory<WebhookDumpDbContext> contextFactory,
  ILogger<LegacyDatabaseImporter> logger
)
{
  private const string MigratedSuffix = ".migrated";
  private const string UnreadableSuffix = ".unreadable";

  // SQLITE_CORRUPT and SQLITE_NOTADB: the file will never be readable, unlike a busy or locked one.
  private const int SqliteCorrupt = 11;
  private const int SqliteNotADatabase = 26;

  private static readonly string[] SidecarSuffixes = ["-wal", "-shm"];

  public async Task ImportAsync(string legacyDbPath, CancellationToken cancellationToken = default)
  {
    if (!File.Exists(legacyDbPath)) return;

    try
    {
      await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
      if (await context.Entries.AnyAsync(cancellationToken).ConfigureAwait(false))
      {
        LogSkippedImport(logger, legacyDbPath);
      }
      else
      {
        List<WebhookDumpEntry> entries;
        try
        {
          entries = await ReadLegacyEntriesAsync(legacyDbPath, cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteNotADatabase or SqliteCorrupt)
        {
          // Reading it again on the next start would fail the same way, so it is moved out of the way instead.
          LogUnreadable(logger, ex, legacyDbPath);
          MoveAside(legacyDbPath, UnreadableSuffix);
          return;
        }

        // One save is one transaction, so a failed carry-over leaves the new database empty and is tried again.
        context.Entries.AddRange(entries);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogImported(logger, entries.Count, legacyDbPath);
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogFailedToImport(logger, ex, legacyDbPath);
      return;
    }

    MoveAside(legacyDbPath, MigratedSuffix);
  }

  private void MoveAside(string legacyDbPath, string suffix)
  {
    try
    {
      var targetPath = GetTargetPath(legacyDbPath, suffix);
      File.Move(legacyDbPath, targetPath);
      foreach (var sidecarSuffix in SidecarSuffixes)
      {
        if (File.Exists(legacyDbPath + sidecarSuffix))
          File.Move(legacyDbPath + sidecarSuffix, targetPath + sidecarSuffix);
      }

      LogRenamed(logger, legacyDbPath, targetPath);
    }
    catch (IOException ex)
    {
      LogFailedToRename(logger, ex, legacyDbPath);
    }
    catch (UnauthorizedAccessException ex)
    {
      LogFailedToRename(logger, ex, legacyDbPath);
    }
  }

  private static async Task<List<WebhookDumpEntry>> ReadLegacyEntriesAsync(string legacyDbPath,
    CancellationToken cancellationToken)
  {
    // Opened read-write so SQLite checkpoints any leftover WAL into the file when it closes, and without pooling so
    // no connection keeps the file open once it is read, which would stop it from being renamed on Windows.
    var connectionString = new SqliteConnectionStringBuilder
    {
      DataSource = legacyDbPath,
      Mode = SqliteOpenMode.ReadWrite,
      Pooling = false
    }.ToString();

    var options = new DbContextOptionsBuilder().UseSqlite(connectionString).Options;
    await using var legacyContext = new DbContext(options);

    var hasTable = await legacyContext.Database
      .SqlQueryRaw<int>(
        "SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'WebhookDumpEntries'")
      .SingleAsync(cancellationToken)
      .ConfigureAwait(false);
    if (hasTable == 0) return [];

    var rows = await legacyContext.Database
      .SqlQueryRaw<LegacyEntry>(
        "SELECT VideoFileId, DiscordMessageId, InsertionTimestamp FROM WebhookDumpEntries WHERE VideoFileId > 0")
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    return rows.Select(row => new WebhookDumpEntry
    {
      VideoFileId = row.VideoFileId,
      DiscordMessageId = row.DiscordMessageId,
      InsertionTimestamp = ParseTimestamp(row.InsertionTimestamp)
    }).ToList();
  }

  // The old database kept the round-trip ("O") format, but a hand-edited row may hold any ISO 8601 date, read as UTC
  // when it has no offset. An entry without a readable date is kept as if it was added now, so it gets the whole
  // retention period rather than being cleaned up on the next run.
  private static DateTime ParseTimestamp(string? value)
  {
    if (DateTimeOffset.TryParseExact(value, "O", DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind,
          out var timestamp) ||
        DateTimeOffset.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal,
          out timestamp))
      return timestamp.UtcDateTime;

    return DateTime.UtcNow;
  }

  private static string GetTargetPath(string legacyDbPath, string suffix)
  {
    var targetPath = legacyDbPath + suffix;
    return File.Exists(targetPath)
      ? $"{legacyDbPath}.{DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}{suffix}"
      : targetPath;
  }

  private sealed class LegacyEntry
  {
    public int VideoFileId { get; set; }
    public long? DiscordMessageId { get; set; }
    public string? InsertionTimestamp { get; set; }
  }

  #region LoggerMessages

  [LoggerMessage(LogLevel.Information,
    "Carried over {Count} entries from the old plugin database \"{FilePath}\" into the server's plugin database.")]
  static partial void LogImported(ILogger<LegacyDatabaseImporter> logger, int count, string filePath);

  [LoggerMessage(LogLevel.Warning,
    "Found the old plugin database \"{FilePath}\", but the server's plugin database already has entries, so nothing was carried over.")]
  static partial void LogSkippedImport(ILogger<LegacyDatabaseImporter> logger, string filePath);

  [LoggerMessage(LogLevel.Error,
    "Failed to carry over the old plugin database \"{FilePath}\"; it was left in place and will be read again on the next start.")]
  static partial void LogFailedToImport(ILogger<LegacyDatabaseImporter> logger, Exception ex, string filePath);

  [LoggerMessage(LogLevel.Error,
    "The old plugin database \"{FilePath}\" is not a SQLite database or is corrupt, so nothing was carried over; it is kept with an \".unreadable\" suffix and can be deleted.")]
  static partial void LogUnreadable(ILogger<LegacyDatabaseImporter> logger, Exception ex, string filePath);

  [LoggerMessage(LogLevel.Information, "Renamed the old plugin database \"{FilePath}\" to \"{MigratedPath}\".")]
  static partial void LogRenamed(ILogger<LegacyDatabaseImporter> logger, string filePath, string migratedPath);

  [LoggerMessage(LogLevel.Error,
    "Failed to rename the old plugin database \"{FilePath}\"; it will be looked at again on the next start.")]
  static partial void LogFailedToRename(ILogger<LegacyDatabaseImporter> logger, Exception ex, string filePath);

  #endregion
}
