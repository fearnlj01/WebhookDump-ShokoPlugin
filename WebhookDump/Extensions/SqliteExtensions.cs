using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Shoko.Plugin.WebhookDump.Extensions;

[SuppressMessage("Naming", "CA1708:Identifiers should differ by more than case")]
public static class SqliteExtensions
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

  extension(SqliteDataReader reader)
  {
    public bool TryGetDateTimeOffset(int index, out DateTimeOffset result)
    {
      result = DateTimeOffset.UtcNow;
      if (reader.IsDBNull(index))
        return false;

      var dateString = reader.GetString(index);

      return DateTimeOffset.TryParseExact(
        dateString,
        "O",
        DateTimeFormatInfo.InvariantInfo,
        DateTimeStyles.RoundtripKind,
        out result
      );
    }
  }
}
