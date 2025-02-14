using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NLog;
using Shoko.Plugin.WebhookDump.Models.Discord;
using Shoko.Plugin.WebhookDump.Settings;

namespace Shoko.Plugin.WebhookDump.API;

public class DiscordClient
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  private readonly HttpClient _httpClient;
  private readonly IOptionsMonitor<WebhookSettings> _webhookOptionsMonitor;

  public DiscordClient(HttpClient httpClient, IOptionsMonitor<WebhookSettings> webhookOptionsMonitor)
  {
    _httpClient = httpClient;
    _webhookOptionsMonitor = webhookOptionsMonitor;

    httpClient.BaseAddress =
      new Uri(WebhookSettings.Url.EndsWith('/') ? WebhookSettings.Url : WebhookSettings.Url + '/');
  }

  private WebhookSettings WebhookSettings => _webhookOptionsMonitor.CurrentValue;

  public async Task<MinimalMessageState?> SendWebhook(Message webhook)
  {
    var uri = new UriBuilder(_httpClient.BaseAddress!.ToString().TrimEnd('/')) { Query = "wait=true" }.Uri;

    try
    {
      var response = await _httpClient.PostAsJsonAsync(uri, webhook, SerializerOptions).ConfigureAwait(false);
      if (response.IsSuccessStatusCode)
        return await response.Content.ReadFromJsonAsync<MinimalMessageState>(SerializerOptions).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Exception thrown when sending a webhook to discord");
      Logger.Debug("Exception: {ex}", ex);
    }

    return null;
  }

  private async Task<bool> PatchWebhook(string messageId, Message webhook)
  {
    var uri = new Uri($"messages/{messageId}", UriKind.Relative);

    try
    {
      var response = await _httpClient.PatchAsJsonAsync(uri, webhook, SerializerOptions).ConfigureAwait(false);
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture,
        "Exception thrown when patching a previous webhook message (MessageId={id})", messageId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return false;
  }

  public async Task<bool> PatchWebhook(string messageId, Message webhook, Stream? imageStream)
  {
    if (imageStream == null) return await PatchWebhook(messageId, webhook).ConfigureAwait(false);

    var uri = new Uri($"messages/{messageId}", UriKind.Relative);
    var form = new MultipartFormDataContent();
    var json = JsonSerializer.Serialize(webhook, SerializerOptions);

    var jsonPayload = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
    var imagePayload = new StreamContent(imageStream)
    {
      Headers = { ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Image.Jpeg) }
    };

    form.Add(jsonPayload, "payload_json");
    form.Add(imagePayload, "files[0]", "unknown.jpg");

    try
    {
      var response = await _httpClient.PatchAsync(uri, form).ConfigureAwait(false);
      return response.IsSuccessStatusCode;
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture,
        "Exception thrown when patching a previous webhook message (MessageId={id})", messageId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return false;
  }

  public async Task<MinimalMessageState?> GetWebhookMessageState(string messageId)
  {
    var uri = new Uri($"messages/{messageId}", UriKind.Relative);

    try
    {
      return await _httpClient.GetFromJsonAsync<MinimalMessageState>(uri, SerializerOptions).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      Logger.Error(CultureInfo.InvariantCulture, "Unable to retreive discord message status (MessageId={id})",
        messageId);
      Logger.Debug("Exception: {ex}", ex);
    }

    return null;
  }
}
