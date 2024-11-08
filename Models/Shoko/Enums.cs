using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming

namespace Shoko.Plugin.WebhookDump.Models.Shoko;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageSource
{
  AniDB = 1,
  TvDB = 2,
  TMDB = 3,
  User= 99,
  Shoko = 100,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageType
{
  Poster = 1,
  Banner = 2,
  Thumbnail = 3,
  Backdrop = 4,
  Character = 5,
  Staff = 6,
  Logo = 7,
  Avatar = 99
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeriesType
{
  Unknown = 0,
  Other = 1,
  TV = 2,
  TVSpecial = 3,
  Web = 4,
  Movie = 5,
  OVA = 6
}
