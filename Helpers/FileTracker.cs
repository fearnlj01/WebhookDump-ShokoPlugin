using System.Collections.Generic;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.WebhookDump.Apis;

public class FileTracker
{
  private readonly Dictionary<int, IVideoFile> _fileSet;

  public FileTracker()
  {
    _fileSet = new();
  }

  public bool TryAddFile(IVideoFile fileInfo)
  {
    return _fileSet.TryAdd(fileInfo.VideoID, fileInfo);
  }

  public bool TryRemoveFile(int fileId)
  {
    return _fileSet.Remove(fileId);
  }

  public bool Contains(int fileId)
  {
    return _fileSet.ContainsKey(fileId);
  }

  public bool TryGetValue(int fileId, out IVideoFile fileInfo)
  {
    return _fileSet.TryGetValue(fileId, out fileInfo);
  }
}
