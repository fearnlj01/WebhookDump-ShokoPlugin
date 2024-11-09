using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models.Shoko;
using Shoko.Plugin.WebhookDump.Models.Shoko.AniDB;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.Apis;

public sealed class ShokoHelper : IDisposable
{
  private readonly HttpClient _httpClient;
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

  public ShokoHelper(SettingsProvider settingsProvider)
  {
    var settings = settingsProvider.GetSettings();

    _httpClient = new HttpClient
    {
      BaseAddress = new Uri($"http://localhost:{settings.Shoko.ServerPort}/api/v3/"),
      DefaultRequestHeaders =
      {
        {"Accept", "*/*"},
        {"apikey", settings.Shoko.ApiKey}
      },
    };
  }

  public async Task DumpFile(int fileId)
  {
    try
    {
      Logger.Info(CultureInfo.InvariantCulture, "Plugin triggering automatic AVDump (fileId={fileId})", fileId);

      var response = await _httpClient.PostAsJsonAsync("AVDump/DumpFiles", new
      {
        FileIDs = new[] { fileId },
        Priority = false
      });
      _ = response.EnsureSuccessStatusCode();
    }
    catch (Exception ex)
    {
      Logger.Warn(CultureInfo.InvariantCulture, "Failed to process AVDump request (fileId={fileId})", fileId);
      Logger.Debug("Exception: {ex}", ex);
    }
  }

  public async Task<AniDBSearchResult> MatchTitle(string filename)
  {
    try
    {
      var title = GetSafeTitleFromFile(filename);

      var response = await _httpClient.GetAsync($"Series/AniDB/Search?query={title}&pageSize=10&page=1");
      _ = response.EnsureSuccessStatusCode();

      return await response.Content.ReadFromJsonAsync<AniDBSearchResult>();
    }
    catch (Exception ex) when (
      ex is HttpRequestException or JsonException or ArgumentNullException or InvalidOperationException
    )
    {
      Logger.Warn(CultureInfo.InvariantCulture, "Unable to retrieve title information for a file (fileName='{filename}') from AniDB", filename);
      Logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task ScanFile(IVideo video, int matchAttempts)
  {
    Logger.Info(CultureInfo.InvariantCulture, "Requesting file rescan (fileId={fileID}, matchAttempts={matchAttempts})", video.ID, matchAttempts);
    try
    {
      var response = await _httpClient.PostAsync($"File/{video.ID}/Rescan", null);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      Logger.Warn($"Unable to scan file ('{video.EarliestKnownName}')");
      Logger.Warn("Exception: {exception}", ex);
    }
  }

  public async Task ScanFileById(int fileId)
  {
    try
    {
      Logger.Info(CultureInfo.InvariantCulture, "Requesting file rescan (fileId={fileID})", fileId);

      var response = await _httpClient.PostAsync($"File/{fileId}/Rescan", null);
      _ = response.EnsureSuccessStatusCode();
    }
    catch (HttpRequestException ex)
    {
      Logger.Warn(CultureInfo.InvariantCulture, "Unable to scan file by ID ('{fileId}')", fileId);
      Logger.Warn("Exception: {exception}", ex);
    }
  }

  public async Task<Image> GetSeriesPoster(ISeries anime)
  {
    try
    {
      var response = await _httpClient.GetAsync($"Series/{anime.ID}/Images/Poster");
      _ = response.EnsureSuccessStatusCode();

      return await response.Content.ReadFromJsonAsync<Image>();
    }
    catch (HttpRequestException ex)
    {
      Logger.Warn(CultureInfo.InvariantCulture, "Poster could not be downloaded for series ID: {animeId}", anime.ID);
      Logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  public async Task<MemoryStream> GetImageStream(Image shokoImage)
  {
    try
    {
      var response = await _httpClient.GetAsync($"Image/{shokoImage.Source}/{shokoImage.Type}/{shokoImage.ID}");
      _ = response.EnsureSuccessStatusCode();

      MemoryStream stream = new();

      await using var responseStream = await response.Content.ReadAsStreamAsync();
      await responseStream.CopyToAsync(stream);

      _ = stream.Seek(0, SeekOrigin.Begin);
      return stream;
    }
    catch (HttpRequestException ex)
    {
      Logger.Warn(CultureInfo.InvariantCulture, "Could not retreieve image for the primary {poster.Source} {poster.Type} of {poster.ID}", shokoImage.Source, shokoImage.Type, shokoImage.ID);
      Logger.Debug("Exception: {ex}", ex);
      return null;
    }
  }

  private static string GetSafeTitleFromFile(string file)
  {
    var regex = new Regex(@"^((\[.*?\]\s*)*)(?<SeriesName>.+(?= - ))(.*)$");

    var match = regex.Match(file);
    var output = match.Success ? match.Groups["SeriesName"].Value : null;
    return WebUtility.UrlEncode(output);
  }

  #region Disposal
  private bool _disposed;
  ~ShokoHelper()
  {
    Dispose(false);
  }
  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  private void Dispose(bool disposing)
  {
    if (_disposed) return;
    if (disposing)
    {
      _httpClient.Dispose();
    }
    _disposed = true;
  }
  #endregion Disposal
}
