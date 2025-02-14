namespace Shoko.Plugin.WebhookDump.Models.Shoko.Common;

public class ListResult<T>
{
  public int Total { get; set; }
  public List<T> List { get; set; } = [];
}
