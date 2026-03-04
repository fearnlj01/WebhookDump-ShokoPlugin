namespace Shoko.Plugin.WebhookDump.Persistence;

public interface ICachedDataProxy : ICachedData
{
  bool TrySetDatabase(ICachedData inner);
  bool TrySetException(Exception ex);
}
