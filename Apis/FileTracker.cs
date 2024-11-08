using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.WebhookDump.Apis;

public class FileTracker
{
  private readonly Dictionary<int, IVideo> _fileSet = new();

  public bool TryAddFile(IVideo video)
  {
    return _fileSet.TryAdd(video.ID, video);
  }

  public bool TryRemoveFile(int videoId)
  {
    return _fileSet.Remove(videoId);
  }

  public bool Contains(int videoId)
  {
    return _fileSet.ContainsKey(videoId);
  }

  public bool TryGetValue(int videoId, out IVideo video)
  {
    return _fileSet.TryGetValue(videoId, out video);
  }
}
