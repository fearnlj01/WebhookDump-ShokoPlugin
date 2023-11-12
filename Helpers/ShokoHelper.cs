using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
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

  public async Task DumpFile(int fileId)
  {
    try
    {
      _logger.Info(CultureInfo.InvariantCulture, "Plugin triggering automatic AVDump (fileId={fileId})", fileId);

      HttpResponseMessage response = await _httpClient.PostAsJsonAsync("AVDump/DumpFiles", new
      {
        FileIDs = new[] { fileId },
        Priority = false
      });
      _ = response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      _logger.Warn(CultureInfo.InvariantCulture, "Failed to process AVDump request (fileId={fileId})", fileId);
      _logger.Debug("Exception: {ex}", ex);
    }
  }

  public async Task<AniDBSearchResult> MatchTitle(string filename)
  {
    try
    {
      string title = GetSafeTitleFromFile(filename);

      HttpResponseMessage response = await _httpClient.GetAsync($"Series/AniDB/Search?query={title}&includeTitles=false&pageSize=3&page=1");
      _ = response.EnsureSuccessStatusCode();

      string content = await response.Content.ReadAsStringAsync();
      return JsonSerializer.Deserialize<AniDBSearchResult>(content);
    }
    catch (Exception ex) when (
      ex is HttpRequestException or JsonException or ArgumentNullException or InvalidOperationException
    )
    {
      _logger.Warn(CultureInfo.InvariantCulture, "Unable to retrieve title information for a file (fileName='{filename}') from AniDB", filename);
      _logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task ScanFile(IVideoFile file, int autoMatchAttempts = 1)
  {
    try
    {
      await Task.Delay(TimeSpan.FromMinutes(autoMatchAttempts * 5));

      _logger.Info(CultureInfo.InvariantCulture, "Requesting file rescan (fileId={fileID}, matchAttempts={matchAttempts})", file.VideoFileID, autoMatchAttempts);

      HttpResponseMessage response = await _httpClient.PostAsync($"File/{file.VideoFileID}/Rescan", null);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn($"Unable to scan file ('{file.Filename}')");
      _logger.Warn("Exception: ", ex);
    }
  }

  public async Task ScanFileById(int fileId)
  {
    try
    {
      _logger.Info(CultureInfo.InvariantCulture, "Requesting file rescan (fileId={fileID})", fileId);

      HttpResponseMessage response = await _httpClient.PostAsync($"File/{fileId}/Rescan", null);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn(CultureInfo.InvariantCulture, "Unable to scan file by ID ('{fileId}')", fileId);
      _logger.Warn("Exception: ", ex);
    }
  }

  public async Task<AniDBPoster> GetSeriesPoster(IAnime anime)
  {
    try
    {
      HttpResponseMessage response = await _httpClient.GetAsync($"Series/AniDB/{anime.AnimeID}/Series?includeDataFrom=AniDB");
      _ = response.EnsureSuccessStatusCode();

      using Stream responseStream = await response.Content.ReadAsStreamAsync();
      using JsonDocument jsonDoc = await JsonDocument.ParseAsync(responseStream);

      string image = jsonDoc.RootElement.GetProperty("Images").GetProperty("Posters")[0].GetRawText();
      return JsonSerializer.Deserialize<AniDBPoster>(image);
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn(CultureInfo.InvariantCulture, "Poster could not be downloaded for series ID: {animeId}", anime.AnimeID);
      _logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task<MemoryStream> GetImageStream(AniDBPoster poster)
  {
    try
    {
      HttpResponseMessage response = await _httpClient.GetAsync($"Image/{poster.Source}/{poster.Type}/{poster.ID}");
      _ = response.EnsureSuccessStatusCode();

      MemoryStream stream = new();

      using Stream responseStream = await response.Content.ReadAsStreamAsync();
      await responseStream.CopyToAsync(stream);

      _ = stream.Seek(0, SeekOrigin.Begin);
      return stream;
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn(CultureInfo.InvariantCulture, "Could not retreieve image for the primary {poster.Source} {poster.Type} of {poster.ID}", poster.Source, poster.Type, poster.ID);
      _logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  private static string GetSafeTitleFromFile(string file)
  {
    string regex = @"^((\[.*?\]\s*)*)(.+(?= - ))(.*)$";

    Match results = Regex.Match(file, regex);
    string output = results.Success ? results.Groups[3].Value : file;
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
      if (disposing)
      {
        _httpClient.Dispose();
      }

      _disposed = true;
    }
  }
  #endregion
}
