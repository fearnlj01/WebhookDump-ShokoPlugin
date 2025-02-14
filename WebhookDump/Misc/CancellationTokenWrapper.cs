namespace Shoko.Plugin.WebhookDump.Misc;

public class CancellationTokenWrapper<T>(
  T key,
  Dictionary<T, CancellationTokenSource> tokenDict,
  CancellationTokenSource cts) : IDisposable where T : notnull
{
  public CancellationToken Token => cts.Token;

  public void Dispose()
  {
    if (!tokenDict.TryGetValue(key, out var cancellationTokenSource)) return;
    cancellationTokenSource.Dispose();
    tokenDict.Remove(key);
    GC.SuppressFinalize(this);
  }
}
