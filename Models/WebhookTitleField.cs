using Shoko.Plugin.WebhookDump.Models.AniDB;

namespace Shoko.Plugin.WebhookDump.Models;

public class WebhookTitleField : IWebhookField
{
	private static AniDBSeries _series;
  public WebhookTitleField(AniDBSeries series)
  {
    _series = series;

    Name = "AniDB Link";
    Value = $"[{_series.Title}](https://anidb.net/anime/{_series.ID}/release/add)";
  }

  public string Name { get; }
  public string Value { get; }
  public bool Inline { get; } = true;
}