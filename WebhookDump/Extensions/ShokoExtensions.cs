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
    public string GetMarkdownSanitizedEd2K()
    {
      return $"ed2k://|file|{video.EarliestKnownName.EscapeMarkdownPairs()}|{video.Size}|{video.ED2K}|/";
    }

    public string? GetCrc32()
    {
      return video.Hashes.FirstOrDefault(hd => hd.Type == "CRC32")?.Value;
    }
  }

  extension(ISeries series)
  {
    public bool IsCurrentlyAiring()
    {
      return series.AirDate.HasValue &&
             (
               (series.EndDate.HasValue && series.AirDate.Value <= DateTime.Now && series.EndDate.Value >= DateTime.Now)
               || (!series.EndDate.HasValue && series.AirDate.Value <= DateTime.Now)
             );
    }
  }

  extension(IAnidbAnimeSearchResult anime)
  {
    public bool IsCurrentlyAiring()
    {
      return anime.AnidbAnime?.IsCurrentlyAiring() ??
             (anime.ShokoSeries is not null && anime.ShokoSeries.IsCurrentlyAiring());
    }
  }
}
