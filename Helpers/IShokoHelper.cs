using System.IO;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Models.AniDB;

namespace Shoko.Plugin.WebhookDump.Apis;

public interface IShokoHelper
{
  void Dispose();
  Task<AVDumpResult> DumpFile(IVideoFile file, int attemptCount = 1);
  Task<MemoryStream> GetImageStream(AniDBPoster poster);
  Task<AniDBPoster> GetSeriesPoster(IAnime anime);
  Task<AniDBSearchResult> MatchTitle(IVideoFile file);
  Task ScanFile(IVideoFile file, int autoMatchAttempts = 1);
}
