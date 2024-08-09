using System.IO;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.WebhookDump.Models.AniDB;

namespace Shoko.Plugin.WebhookDump.Apis;

public interface IShokoHelper
{
  void Dispose();
  Task DumpFile(int fileId);
  Task<MemoryStream> GetImageStream(AniDBPoster poster);
  Task<AniDBPoster> GetSeriesPoster(IShokoSeries anime);
  Task<AniDBSearchResult> MatchTitle(string filename);
  Task ScanFile(IVideoFile file, int autoMatchAttempts = 1);
  Task ScanFileById(int fileId);
}
