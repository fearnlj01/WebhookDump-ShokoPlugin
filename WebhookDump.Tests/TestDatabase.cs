using Microsoft.EntityFrameworkCore;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Tests;

// A migrated plugin database in a SQLite file of its own, in a folder that is removed afterwards.
public sealed class TestDatabase : IDbContextFactory<WebhookDumpDbContext>, IAsyncDisposable
{
  private readonly DbContextOptions<WebhookDumpDbContext> _options;

  private TestDatabase(string folder)
  {
    Folder = folder;
    _options = new DbContextOptionsBuilder<WebhookDumpDbContext>()
      .UseSqlite($"Data Source={Path.Combine(folder, "WebhookDump.db3")};Pooling=False")
      .Options;
  }

  public string Folder { get; }

  public static async Task<TestDatabase> CreateAsync()
  {
    var folder = Path.Combine(Path.GetTempPath(), $"webhookdump-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);

    var database = new TestDatabase(folder);
    await using var context = database.CreateDbContext();
    await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    return database;
  }

  public WebhookDumpDbContext CreateDbContext()
  {
    return new WebhookDumpDbContext(_options);
  }

  public ValueTask DisposeAsync()
  {
    Directory.Delete(Folder, true);
    return ValueTask.CompletedTask;
  }
}
