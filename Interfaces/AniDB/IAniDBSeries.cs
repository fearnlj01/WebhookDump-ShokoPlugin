namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public interface IAniDBSeries
{
  public int ID { get; set; }
	public int? ShokoID { get; set; }
	public string Type { get; set; }
	public string Title { get; set; }
	//  public string? Titles { get; set; }
	// Titles are not being implemented in this plugin for now, we will simply have to live with the x-jat name.
	public string Description { get; set; }
	public bool Restricted { get; set; }
	public AniDBPoster Poster { get; set; }
	public int? EpisodeCount { get; set; }
}