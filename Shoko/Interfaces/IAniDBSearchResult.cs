using System.Collections.Generic;
namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public interface IAniDBSearchResult
{
  public int Total { get; set; }
  public List<AniDBSeries> List { get; set; }
}
