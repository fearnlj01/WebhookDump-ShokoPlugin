using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Video;

namespace Shoko.Plugin.WebhookDump.Extensions;

[SuppressMessage("Naming", "CA1708:Identifiers should differ by more than case")]
public static class ShokoExtensions
{
  extension(IVideo video)
  {
    public string? Crc32 => video.Hashes.FirstOrDefault(hd => hd.Type == "CRC32")?.Value;

    public string MarkdownSanitizedEd2K =>
      $"ed2k://|file|{video.EarliestKnownName.EscapeMarkdownPairs()}|{video.Size}|{video.ED2K}|/";
  }

  extension(ISeries series)
  {
    private bool IsCurrentlyAiring => series.AirDate.HasValue &&
                                      DateTime.Now is var now &&
                                      (
                                        (
                                          series.EndDate.HasValue &&
                                          series.AirDate.Value <= now
                                          && series.EndDate.Value >= now
                                        ) ||
                                        (
                                          !series.EndDate.HasValue &&
                                          series.AirDate.Value <= now
                                        )
                                      );
  }

  extension(IAnidbAnimeSearchResult anime)
  {
    public bool IsCurrentlyAiring =>
      anime.AnidbAnime?.IsCurrentlyAiring ?? anime.ShokoSeries?.IsCurrentlyAiring ?? false;

    public bool IsRestricted => anime.AnidbAnime?.Restricted ?? anime.ShokoSeries?.Restricted ?? false;

    public DateTime? AirDate => anime.AnidbAnime?.AirDate ?? anime.ShokoSeries?.AirDate;
  }
}
