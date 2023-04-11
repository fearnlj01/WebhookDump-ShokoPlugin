namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public interface IAniDBSearchResult
{
  public int Total { get; set; }
  public AniDBSeries[] List { get; set; }
}
