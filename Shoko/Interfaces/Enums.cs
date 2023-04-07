using System.Text.Json.Serialization;

namespace Shoko.Plugin.WebhookDump.Models.AniDB;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageSource
{
  AniDB = 1,
  TvDB = 2,
  TMDB = 3,
  Shoko = 100,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageType
{
  Poster = 1,
  Banner = 2,
  Thumb = 3,
  Fanart = 4,
  Character = 5,
  Staff = 6,
  Static = 100
}