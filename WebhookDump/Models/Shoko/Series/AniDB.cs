using System.Text.Json.Serialization;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Converters;
using Shoko.Plugin.WebhookDump.Models.Shoko.Common;
using Shoko.Plugin.WebhookDump.Models.Shoko.Enums;

namespace Shoko.Plugin.WebhookDump.Models.Shoko.Series;

public class AniDB
{
  public int ID { get; init; }
  public int? ShokoID { get; init; }
  public SeriesType Type { get; init; }
  public string? Title { get; init; }
  public IReadOnlyList<Title>? Titles { get; init; }
  public string? Description { get; init; }

  [JsonConverter(typeof(DateOnlyConverter))]
  public DateOnly? AirDate { get; init; }

  [JsonConverter(typeof(DateOnlyConverter))]
  public DateOnly? EndDate { get; init; }

  private static DateOnly Now => DateOnly.FromDateTime(DateTime.Now);

  public bool IsCurrentlyAiring => AirDate.HasValue &&
                                   ((EndDate.HasValue && AirDate.Value <= Now && EndDate.Value >= Now) ||
                                    (!EndDate.HasValue && AirDate.Value <= Now));

  public bool Restricted { get; init; }

  public Image? Poster { get; init; }
  public int? EpisodeCount { get; init; }

  public Rating? Rating { get; init; }
  public Rating? UserApproval { get; init; }
  public RelationType? Relation { get; init; }
}
