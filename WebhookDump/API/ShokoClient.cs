using System.Globalization;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NLog;
using Shoko.Plugin.WebhookDump.Misc;
using Shoko.Plugin.WebhookDump.Models.Shoko.Common;
using Shoko.Plugin.WebhookDump.Models.Shoko.Series;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.API;

public class ShokoClient
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
  private readonly HttpClient _httpClient;

  public ShokoClient(HttpClient httpClient, IOptions<ShokoSettings> options)
  {
    var settings = options.Value;

    _httpClient = httpClient;

    _httpClient.BaseAddress = new Uri($"http://localhost:{settings.ServerPort}/api/v3/");

    _httpClient.DefaultRequestHeaders.Add("Accept", MediaTypeNames.Application.Json);
    _httpClient.DefaultRequestHeaders.Add("apikey", settings.ApiKey);
  }

  public async Task<bool> DumpFile(int fileId)
  {
    var uri = new Uri("AVDump/DumpFiles", UriKind.Relative);
    var requestBody = new DumpFilesBody(fileId);

    try
    {
      var response = await _httpClient.PostAsJsonAsync(uri, requestBody).ConfigureAwait(false);
      if (response.IsSuccessStatusCode) return true;
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown when dumping a file (FileId={fileId})", fileId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return false;
  }

  public async Task<bool> ScanFile(int fileId)
  {
    var uri = new Uri($"File/{fileId}/Rescan", UriKind.Relative);

    try
    {
      var response = await _httpClient.PostAsync(uri, null).ConfigureAwait(false);
      if (response.IsSuccessStatusCode) return true;
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown when re-scanning a file (FileId={fileId})", fileId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return false;
  }

  public async Task<ListResult<AniDB>> MatchTitle(string filename)
  {
    var title = StringHelper.ExtractTitle(filename);
    var uri = new Uri($"Series/AniDB/Search?query={title}&pageSize=10", UriKind.Relative);

    try
    {
      var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
      if (response.IsSuccessStatusCode)
      {
        var result = await response.Content.ReadFromJsonAsync<ListResult<AniDB>>(JsonOptions).ConfigureAwait(false);
        if (result is not null) return result;
      }
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown when matching a title (Filename={filename})",
        filename);
      Logger.Debug("Exception: {ex}", ex);
    }

    return new ListResult<AniDB>();
  }

  public async Task<Image?> GetSeriesPoster(int seriesId)
  {
    var uri = new Uri($"Series/{seriesId}/Images/Poster", UriKind.Relative);

    try
    {
      var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
      if (response.IsSuccessStatusCode)
        return await response.Content.ReadFromJsonAsync<Image?>(JsonOptions).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown searching for a series poster (SeriesId={seriesId})",
        seriesId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return null;
  }

  public async Task<MemoryStream?> GetImageStream(Image shokoImage)
  {
    var uri = new Uri($"Image/{shokoImage.Source}/{shokoImage.Type}/{shokoImage.ID}", UriKind.Relative);

    try
    {
      var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
      if (response.IsSuccessStatusCode)
      {
        await using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var outputStream = new MemoryStream();
        await responseStream.CopyToAsync(outputStream).ConfigureAwait(false);
        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream;
      }
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown fetching an image (ImageId={shokoImage.ID})",
        shokoImage.ID);
      Logger.Debug("Exception: {ex}", ex);
    }

    return null;
  }

  private sealed class DumpFilesBody(int fileId)
  {
    public IList<int> FileIDs { get; init; } = [fileId];
    public bool Priority { get; init; }
  }
}
