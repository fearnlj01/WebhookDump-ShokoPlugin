using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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
      var requestObject = new
      {
        FileIDs = new[] { fileId },
        Priority = false
      };
      var json = new StringContent(JsonSerializer.Serialize(requestObject), Encoding.UTF8, "application/json");

      HttpResponseMessage response = await _httpClient.PostAsync("AVDump/DumpFiles", json);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      _logger.Warn("Exception: {ex}", ex);
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
      _logger.Warn($"Unable to retrieve information about the file ('{filename}') from AniDB");
      _logger.Warn("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task ScanFile(IVideoFile file, int autoMatchAttempts = 1)
  {
    try
    {
      await Task.Delay(TimeSpan.FromMinutes(autoMatchAttempts * 5));

      HttpRequestMessage request = new(HttpMethod.Post, $"File/{file.VideoFileID}/Rescan");

      HttpResponseMessage response = await _httpClient.SendAsync(request);
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
      HttpRequestMessage request = new(HttpMethod.Post, $"File/{fileId}/Rescan");

      HttpResponseMessage response = await _httpClient.SendAsync(request);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      _logger.Warn($"Unable to scan file by ID ('{fileId}')");
      _logger.Warn("Exception: ", ex);
    }
  }

  public async Task<AniDBPoster> GetSeriesPoster(IAnime anime)
  {
    try
    {
      HttpResponseMessage response = await _httpClient.GetAsync($"Series/AniDB/{anime.AnimeID}/Series?includeDataFrom=AniDB");
      _ = response.EnsureSuccessStatusCode();

      string content = await response.Content.ReadAsStringAsync();

      using JsonDocument jsonDoc = JsonDocument.Parse(content);
      string image = jsonDoc.RootElement.GetProperty("Images").GetProperty("Posters")[0].GetRawText();
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
      _logger.Warn($"Could not retreieve image for the primary {poster.Source} {poster.Type} of {poster.ID}");
      _logger.Warn("Exception: {ex}", ex);
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
