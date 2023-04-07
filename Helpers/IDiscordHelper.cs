using System.IO;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Models.AniDB;

namespace Shoko.Plugin.WebhookDump.Apis;

public interface IDiscordHelper
{
  void Dispose();
  Task PatchWebhook(IVideoFile file, IAnime anime, IEpisode episode, MemoryStream imageStream, string messageId);
  Task<string> SendWebhook(IVideoFile file, AVDumpResult dumpResult, AniDBSearchResult searchResult);
  Task<bool> GetMessageReactionBool(string messageId);
}
