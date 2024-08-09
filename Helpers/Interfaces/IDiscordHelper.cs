using System.IO;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.WebhookDump.Models.AniDB;

namespace Shoko.Plugin.WebhookDump.Apis;

public interface IDiscordHelper
{
  void Dispose();
  Task PatchWebhook(IVideoFile file, IShokoSeries anime, IEpisode episode, MemoryStream imageStream, string messageId);
  Task<string> SendWebhook(IVideoFile file, string dumpResult, AniDBSearchResult searchResult);
  Task<bool> GetMessageReactionState(string messageId);
}
