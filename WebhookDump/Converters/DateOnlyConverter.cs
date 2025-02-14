using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Converters;

public class DateOnlyConverter : JsonConverter<DateOnly?>
{
  private const string DateFormat = "yyyy-MM-dd";

  public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.String) return null;

    var dateString = reader.GetString() ?? string.Empty;
    return DateOnly.ParseExact(dateString, DateFormat);
  }

  public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
  {
    if (value.HasValue)
      writer.WriteStringValue(value.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
    else
      writer.WriteNullValue();
  }
}
