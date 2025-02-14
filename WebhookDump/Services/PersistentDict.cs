using System.Text.Json;
using Microsoft.Extensions.Hosting;
using NLog;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Models.Discord;

namespace Shoko.Plugin.WebhookDump.Services;

public class PersistentDict<TKey, TValue>(string filePath) : BackgroundService where TKey : notnull
{
  private readonly Dictionary<TKey, TValue> _cache = [];

  public override async Task StartAsync(CancellationToken cancellationToken)
  {
    await LoadFromFileAsync(cancellationToken).ConfigureAwait(false);
    await base.StartAsync(cancellationToken).ConfigureAwait(false);
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    await SaveToFileAsync(cancellationToken).ConfigureAwait(false);
    await base.StopAsync(cancellationToken).ConfigureAwait(false);
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
      RemoveOldEntries();
    }
  }

  public bool Add(TKey key, TValue value)
  {
    return _cache.TryAdd(key, value);
  }

  public bool Remove(TKey key)
  {
    return _cache.Remove(key);
  }

  public bool TryGetValue(TKey key, out TValue? value)
  {
    return _cache.TryGetValue(key, out value);
  }

  public List<KeyValuePair<TKey, TValue>> GetAll()
  {
    return [.. _cache];
  }

  public bool Contains(TKey key)
  {
    return _cache.ContainsKey(key);
  }

  private void RemoveOldEntries()
  {
    var cutOffDateTime = DateTimeOffset.UtcNow.AddDays(-7);
    var toDelete = _cache.Where(kvp =>
      (kvp.Value is IWithDate withDate && withDate.EarliestKnownDate < cutOffDateTime)
      || (kvp.Value is DateTimeOffset offset && offset.Date < cutOffDateTime));
    foreach (var entry in toDelete)
      _cache.Remove(entry.Key);
  }

  private async Task SaveToFileAsync(CancellationToken stoppingToken)
  {
    if (_cache.Count == 0) return;
    RemoveOldEntries();

    await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
    await JsonSerializer.SerializeAsync(stream, _cache, cancellationToken: stoppingToken).ConfigureAwait(false);
  }

  private async Task LoadFromFileAsync(CancellationToken stoppingToken)
  {
    if (!File.Exists(filePath)) return;

    try
    {
      await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
      var loadedCache = await JsonSerializer
        .DeserializeAsync<Dictionary<TKey, TValue>>(stream, cancellationToken: stoppingToken).ConfigureAwait(false);

      if (loadedCache != null)
      {
        foreach (var (k, v) in loadedCache)
          _cache[k] = v;
        LogManager.GetCurrentClassLogger().Info("Loaded {@Count} entries from the filesystem into {@TypeName}",
          loadedCache.Count, GetType().Name);
      }
    }
    catch (IOException)
    {
      return;
    }

    File.Delete(filePath);
    RemoveOldEntries();
  }
}

public class PersistentMessageDict(string filePath) : PersistentDict<int, MinimalMessageState>(filePath);

public class PersistentFileIdDict(string filePath) : PersistentDict<int, DateTimeOffset>(filePath);
