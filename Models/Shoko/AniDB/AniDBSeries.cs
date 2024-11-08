#nullable enable
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.Shoko.AniDB;

// Stripped back implementation of Shoko.Server.API.v3.Models.Shoko.AniDB
// ReSharper disable once InconsistentNaming
public class AniDBSeries
{
  // AniDB ID
  public int ID { get; init; }
  public string Title { get; init; } = string.Empty;

  [JsonConverter(typeof(DateOnlyConverter))]
  public DateOnly? AirDate { get; init; }

  [JsonConverter(typeof(DateOnlyConverter))]
  public DateOnly? EndDate { get; init; }

  public bool IsCurrentlyAiring =>
    AirDate.HasValue && ((EndDate.HasValue && AirDate.Value <= Now && EndDate.Value >= Now) ||
                         (!EndDate.HasValue && AirDate.Value <= Now));

  private static DateOnly Now => DateOnly.FromDateTime(DateTime.Now);
  public bool Restricted { get; init; }
}

// Forgive me, I only need this here and do not wish to think of where I want this to go
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
