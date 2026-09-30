using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shoko.Plugin.WebhookDump.Persistence;

namespace Shoko.Plugin.WebhookDump.Design;

// The server configures the contexts at runtime; these only build them for adding migrations. No database server is
// needed: the connection strings are never opened, and the MySQL version only picks the SQL the migrations are
// written for.

public class WebhookDumpDbContextFactory : IDesignTimeDbContextFactory<WebhookDumpDbContext>
{
  public WebhookDumpDbContext CreateDbContext(string[] args)
  {
    return new WebhookDumpDbContext(new DbContextOptionsBuilder<WebhookDumpDbContext>()
      .UseSqlite("Data Source=design-time.db3")
      .Options);
  }
}

public class MySqlWebhookDumpDbContextFactory : IDesignTimeDbContextFactory<MySqlWebhookDumpDbContext>
{
  public MySqlWebhookDumpDbContext CreateDbContext(string[] args)
  {
    return new MySqlWebhookDumpDbContext(new DbContextOptionsBuilder<WebhookDumpDbContext>()
      .UseMySql("Server=localhost", new MySqlServerVersion(new Version(8, 0)))
      .Options);
  }
}

public class SqlServerWebhookDumpDbContextFactory : IDesignTimeDbContextFactory<SqlServerWebhookDumpDbContext>
{
  public SqlServerWebhookDumpDbContext CreateDbContext(string[] args)
  {
    return new SqlServerWebhookDumpDbContext(new DbContextOptionsBuilder<WebhookDumpDbContext>()
      .UseSqlServer("Server=localhost")
      .Options);
  }
}

public static class Program
{
  public static void Main()
  {
  }
}
