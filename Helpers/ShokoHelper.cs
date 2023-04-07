using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Models.AniDB;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Apis;

public class ShokoHelper : IDisposable, IShokoHelper
{
  private readonly HttpClient _httpClient;
  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
  private readonly ISettings _settings;
  private readonly ISettingsProvider _settingsProvider;

  public ShokoHelper(ISettingsProvider settingsProvider)
  {
    _settingsProvider = settingsProvider;
    _settings = _settingsProvider.GetSettings();

    _httpClient = new()
    {
      BaseAddress = new Uri($"http://localhost:{_settings.Shoko.ServerPort}/api/v3/"),
      DefaultRequestHeaders =
      {
        {"Accept", "*/*"},
        {"apikey", _settings.Shoko.ApiKey}
      },
    };
  }

  public async Task<AVDumpResult> DumpFile(IVideoFile file, int attemptCount = 1)
  {
    int maxAttempts = 3;
    try
    {
      HttpRequestMessage request = new(HttpMethod.Post, $"File/{file.VideoFileID}/AVDump");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();

      return JsonSerializer.Deserialize<AVDumpResult>(content);
    }
    catch (Exception ex) when (
      ex is HttpRequestException ||
      ex is JsonException ||
      ex is ArgumentNullException ||
      ex is InvalidOperationException
    )
    {
      if (attemptCount < maxAttempts)
      {
        _logger.Warn($"Error automatically AVDumping file, attempt {attemptCount} of {maxAttempts} ('{file.Filename}')");
        await Task.Delay(5000);
        return await DumpFile(file, attemptCount + 1);
      }
      else
      {
        _logger.Warn($"Error automatically AVDumping file, attempt {attemptCount} of {maxAttempts} ('{file.Filename}')");
        _logger.Warn("Returned exception {e}", ex);
        return null;
      }
    }
  }

  public async Task<AniDBSearchResult> MatchTitle(IVideoFile file)
  {
    try
    {
      var title = GetSafeTitleFromFile(file);
      var response = await _httpClient.GetAsync($"Series/AniDB/Search/{title}?includeTitles=false&pageSize=3&page=1");
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<AniDBSearchResult>(content);
    }
    catch (Exception ex) when (
      ex is HttpRequestException ||
      ex is JsonException ||
      ex is ArgumentNullException ||
      ex is InvalidOperationException
    )
    {
      _logger.Warn($"Unable to retrieve information about the file ('{file.Filename}') from AniDB");
      _logger.Warn("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task ScanFile(IVideoFile file, int autoMatchAttempts = 1)
  {
    try
    {
      await Task.Delay(autoMatchAttempts * 5 * 60 * 1000);

      var request = new HttpRequestMessage(HttpMethod.Post, $"File/{file.VideoFileID}/Rescan");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn($"Unable to scan file ('{file.Filename}')");
      _logger.Warn("Exception: ", ex);
    }
  }

  public async Task<AniDBPoster> GetSeriesPoster(IAnime anime)
  {
    try
    {
      var response = await _httpClient.GetAsync($"Series/AniDB/{anime.AnimeID}/Series?includeDataFrom=AniDB");
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();

      using var jsonDoc = JsonDocument.Parse(content);
      var image = jsonDoc.RootElement.GetProperty("Images").GetProperty("Posters")[0].GetRawText();
      return JsonSerializer.Deserialize<AniDBPoster>(image);
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn($"Poster could not be downloaded for series ID: {anime.AnimeID}");
      _logger.Warn("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task<MemoryStream> GetImageStream(AniDBPoster poster)
  {
    try
    {
      var response = await _httpClient.GetAsync($"Image/{poster.Source}/{poster.Type}/{poster.ID}");
      response.EnsureSuccessStatusCode();

      MemoryStream stream = new();

      using var responseStream = await response.Content.ReadAsStreamAsync();
      await responseStream.CopyToAsync(stream);

      stream.Seek(0, SeekOrigin.Begin);
      return stream;
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn($"Could not retreieve image for the primary {poster.Source} {poster.Type} of {poster.ID}");
      _logger.Warn("Exception: {ex}", ex);
      return null;
    }
  }

  private static string GetSafeTitleFromFile(IVideoFile file)
  {
    var regex = @"^((\[.*?\]\s*)*)(.+(?= - ))(.*)$";

    Match results = Regex.Match(file.Filename, regex);
    var output = results.Success ? results.Groups[3].Value : file.Filename;
    return WebUtility.UrlEncode(output);
  }

  #region Disposal
  private bool _disposed;

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      if (disposing) _httpClient.Dispose();
      _disposed = true;
    }
  }
  #endregion
}
