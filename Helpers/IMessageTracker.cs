namespace Shoko.Plugin.WebhookDump.Apis;

public interface IMessageTracker
{
  void Dispose();
  bool TryGetValue(int fileId, out string messageId);
  bool TryAddMessage(int fileId, string messageId);
  bool TryRemoveMessage(int fileId);
  bool Contains(int fileId);
}
