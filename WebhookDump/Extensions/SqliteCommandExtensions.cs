using System.Text;
using Microsoft.Data.Sqlite;

namespace Shoko.Plugin.WebhookDump.Extensions;

public static class SqliteCommandExtensions
{
  extension(SqliteCommand command)
  {
    public string AddIntParameterList(string prefix, ReadOnlySpan<int> values)
    {
      if (string.IsNullOrWhiteSpace(prefix))
        throw new ArgumentException("The parameter prefix cannot be null or whitespace.", nameof(prefix));
      if (values.Length == 0)
        throw new ArgumentException("At least one value is required!", nameof(values));

      var stringBuilder = new StringBuilder();

      for (var i = 0; i < values.Length; i++)
      {
        if (i > 0)
          stringBuilder.Append(", ");

        var paramName = $"{prefix}{i}";

        stringBuilder.Append(paramName);
        command.Parameters.AddWithValue(paramName, values[i]);
      }

      return stringBuilder.ToString();
    }
  }
}
