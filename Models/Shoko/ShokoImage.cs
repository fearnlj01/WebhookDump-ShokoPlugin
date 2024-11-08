namespace Shoko.Plugin.WebhookDump.Models.Shoko;

public class Image
{
  public int ID { get; init; }
  public ImageType Type { get; init; }
  public ImageSource Source { get; init; }
  public string LanguageCode { get; init; }
  public string RelativeFilePath { get; init; }
  public bool Preferred { get; init; }
  public bool Disabled { get; init; }
  public int? Width { get; init; }
  public int? Height { get; init; }
}
