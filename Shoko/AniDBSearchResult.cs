namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public class AniDBSearchResult : IAniDBSearchResult
{
  public int Total { get; set; }
  public AniDBSeries[] List { get; set; }

}