namespace Shoko.Plugin.WebhookDump.Models.AniDB;

public class AniDBPoster : IAniDBPoster
{
  public ImageSource Source { get; set; }
  public ImageType Type { get; set; }
  public int ID { get; set; }
  public string RelativeFilePath { get; set; }
  public bool Preferred { get; set; }
  public int? Width { get; set; }
  public int? Height { get; set; }
  public bool Disabled { get; set; }
}
