using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shoko.Plugin.WebhookDump.Persistence;

/**
 * The plugin's database, configured and migrated by the server. This context keeps the SQLite migrations, while the
 * derived contexts below keep the ones for when the server runs on MySQL or SQL Server.
 */
public class WebhookDumpDbContext(DbContextOptions<WebhookDumpDbContext> options) : DbContext(options)
{
  public const string DatabaseName = "WebhookDump";

  public DbSet<WebhookDumpEntry> Entries => Set<WebhookDumpEntry>();

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    ArgumentNullException.ThrowIfNull(configurationBuilder);

    configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    modelBuilder.Entity<WebhookDumpEntry>(entry =>
    {
      entry.ToTable("WebhookDumpEntries");
      entry.HasKey(e => e.VideoFileId);
      entry.Property(e => e.VideoFileId).ValueGeneratedNever();
      entry.HasIndex(e => e.InsertionTimestamp);
    });
  }

  private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc)
  );
}

public class MySqlWebhookDumpDbContext(DbContextOptions<WebhookDumpDbContext> options) : WebhookDumpDbContext(options);

public class SqlServerWebhookDumpDbContext(DbContextOptions<WebhookDumpDbContext> options)
  : WebhookDumpDbContext(options);
