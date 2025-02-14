using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Models.Discord.EmbedFields;

namespace Shoko.Plugin.WebhookDump.Models.Discord;

public class Embed
{
  public const string Type = "rich"; // This is the only available type for a webhook driven embed

  private Embed()
  {
  }

  public string? Title { get; private set; }
  public string? Description { get; private set; }
  public string? Url { get; private set; }
  public DateTimeOffset? Timestamp { get; private set; }
  public int? Color { get; private set; }
  public Footer? Footer { get; private set; }
  public Image? Image { get; private set; }
  public Image? Thumbnail { get; private set; }
  public List<Field>? Fields { get; private set; }

  // ReSharper disable once MemberCanBeMadeStatic.Local
  [SuppressMessage("Performance", "CA1822:Mark members as static")]
  private void Validate()
  {
    // No-op - Going off of Discord's documentation, every value is nullable...
  }

  public static Builder Create()
  {
    return new Builder();
  }

  public class Builder
  {
    private readonly Embed _embed = new();

    public Builder SetTitle(string? title)
    {
      _embed.Title = title;
      return this;
    }

    public Builder SetDescription(string? description)
    {
      _embed.Description = description;
      return this;
    }

    public Builder SetUrl(string? url)
    {
      _embed.Url = url;
      return this;
    }

    public Builder SetTimestamp(DateTimeOffset? timestamp)
    {
      _embed.Timestamp = timestamp;
      return this;
    }

    public Builder SetColor(int? color)
    {
      _embed.Color = color;
      return this;
    }

    public Builder SetColor(string color)
    {
      var colourString = StringHelper.GetHexadecimalColour(color) ??
                         throw new InvalidOperationException(
                           $"Unable to parse input as a hexadecimal colour (Input=${color})");
      _embed.Color = int.Parse(colourString, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

      return this;
    }

    public Builder SetFooter(Footer footer)
    {
      _embed.Footer = footer;
      return this;
    }

    public Builder SetImage(Image? image)
    {
      _embed.Image = image;
      return this;
    }

    public Builder SetThumbnail(Image? thumbnail)
    {
      _embed.Thumbnail = thumbnail;
      return this;
    }

    public Builder SetFields(List<Field> fields)
    {
      _embed.Fields = fields;
      return this;
    }

    public Builder AddField(Field field)
    {
      _embed.Fields ??= [];
      _embed.Fields.Add(field);

      return this;
    }

    public Embed Build()
    {
      _embed.Validate();
      return _embed;
    }
  }
}
