using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Discord.Client;

public partial class DiscordClient
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  private readonly HttpClient _httpClient;
  private readonly ILogger<DiscordClient> _logger;

  public DiscordClient(
    HttpClient httpClient,
    Func<WebhookConfiguration> getWebhookConfiguration,
    ILogger<DiscordClient> logger
  )
  {
    _httpClient = httpClient;
    _logger = logger;
    var configuration = getWebhookConfiguration();

    httpClient.BaseAddress =
      new Uri(configuration.WebhookUrl.EndsWith('/') ? configuration.WebhookUrl : configuration.WebhookUrl + '/');
  }

  public async Task<MinimalMessageState?> SendWebhook(Message webhook)
  {
    var uri = new UriBuilder(_httpClient.BaseAddress!.ToString().TrimEnd('/')) { Query = "wait=true" }.Uri;

    try
    {
      var response = await _httpClient.PostAsJsonAsync(uri, webhook, SerializerOptions).ConfigureAwait(false);
      if (response.IsSuccessStatusCode)
        return await response.Content.ReadFromJsonAsync<MinimalMessageState>(SerializerOptions).ConfigureAwait(false);
    }
    catch
    {
      LogExceptionThrownWhenSendingAWebhookToDiscord();
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
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
    catch
    {
      LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(messageId);
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
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
    catch
    {
      LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(messageId);
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
    }

    return false;
  }

  public async Task<MinimalMessageState?> GetWebhookMessageState(string messageId)
  {
    var uri = new Uri($"messages/{messageId}", UriKind.Relative);
    // TODO: Take advantage of the Shoko connectivity checks to determine if we are connected to the internet

    try
    {
      return await _httpClient.GetFromJsonAsync<MinimalMessageState>(uri, SerializerOptions).ConfigureAwait(false);
    }
    catch
    {
      LogUnableToRetreiveDiscordMessageStatusMessageidId(messageId);
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
    }

    return null;
  }

  [LoggerMessage(LogLevel.Error, "Exception thrown when sending a webhook to discord!")]
  partial void LogExceptionThrownWhenSendingAWebhookToDiscord();

  [LoggerMessage(LogLevel.Debug, "An exception occured in the WebhookDump plugin!")]
  partial void LogAnExceptionOccuredInTheWebhookdumpPlugin();

  [LoggerMessage(LogLevel.Error, "Exception thrown when patching a previous webhook message (MessageId={id})")]
  partial void LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(string id);

  [LoggerMessage(LogLevel.Error, "Unable to retreive discord message status (MessageId={id})")]
  partial void LogUnableToRetreiveDiscordMessageStatusMessageidId(string id);
}
