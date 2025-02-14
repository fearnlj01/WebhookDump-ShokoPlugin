using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.WebhookDump.Models.Shoko.Enums;

namespace Shoko.Plugin.WebhookDump.Models.Shoko.Common;

public class Image
{
  public int ID { get; init; }
  public ImageEntityType Type { get; init; }
  public ImageSource Source { get; init; }
  public string? LanguageCode { get; init; }
  public string? RelativeFilepath { get; init; }
  public bool Preferred { get; init; }
  public bool Disabled { get; init; }
  public int? Width { get; init; }
  public int? Height { get; init; }
  public Rating? CommunityRating { get; init; }
  public ImageSeriesInfo? Series { get; init; }

  public class ImageSeriesInfo
  {
    public int ID { get; init; }
    public string Name { get; init; } = string.Empty;
  }
}
