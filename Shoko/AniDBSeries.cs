namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public class AniDBSeries : IAniDBSeries
{
  public int ID { get; set; }
  public int? ShokoID { get; set; }
  public string Type { get; set; }
  public string Title { get; set; }
  public string Description { get; set; }
  public bool Restricted { get; set; }
  public int? EpisodeCount { get; set; }
  public AniDBPoster Poster { get; set; }
}
