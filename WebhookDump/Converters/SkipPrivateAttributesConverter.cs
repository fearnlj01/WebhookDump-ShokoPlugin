using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shoko.Plugin.WebhookDump.Attributes;

namespace Shoko.Plugin.WebhookDump.Converters;

public class SkipPrivateAttributesConverter<T> : JsonConverter<T>
{
  public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions? options) =>
    JsonSerializer.Deserialize<T>(ref reader, options);

  public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    if (value != null)
      WriteProperties(writer, value, options);
    writer.WriteEndObject();
  }

  private static void WriteProperties(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
  {
    var valueType = value.GetType();

    foreach (var property in valueType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
      var propertyName = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
      var propertyType = property.PropertyType;

      writer.WritePropertyName(propertyName);

      if (Attribute.IsDefined(property, typeof(JsonPrivateAttribute)))
      {
        writer.WriteStringValue("CENSORED");
        continue;
      }

      var propertyValue = property.GetValue(value);
      if (propertyValue == null)
      {
        writer.WriteNullValue();
        continue;
      }

      if (IsSimpleType(propertyType))
      {
        JsonSerializer.Serialize(writer, propertyValue, propertyType, options);
        continue;
      }

      // At this point, the next property is a new class.
      writer.WriteStartObject();
      WriteProperties(writer, propertyValue, options);
      writer.WriteEndObject();
    }
  }

  private static bool IsSimpleType(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                                                 type == typeof(decimal) || type == typeof(DateTime) ||
                                                 type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
                                                 type == typeof(Guid);
}
