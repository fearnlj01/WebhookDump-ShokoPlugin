using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Services;
using Shoko.Plugin.WebhookDump.Configurations;
using Shoko.Plugin.WebhookDump.Discord.Models;

namespace Shoko.Plugin.WebhookDump.Discord.Client;

public partial class DiscordClient : IDisposable
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
  };

  private readonly IConnectivityService _connectivityService;

  private readonly HttpClient _httpClient;
  private readonly ILogger<DiscordClient> _logger;
  private readonly Lock _onlineStateLock = new();

  private volatile bool _currentlyOnline;
  private TaskCompletionSource _onlineTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public DiscordClient(
    HttpClient httpClient,
    ConfigurationProvider<PluginConfiguration> pluginConfigurationProvider,
    IConnectivityService connectivityService,
    ILogger<DiscordClient> logger
  )
  {
    _httpClient = httpClient;
    _connectivityService = connectivityService;
    _logger = logger;
    var webhookUrl = pluginConfigurationProvider.Load().Webhook.WebhookUrl;

    httpClient.BaseAddress =
      new Uri(webhookUrl.EndsWith('/') ? webhookUrl : webhookUrl + '/');

    _currentlyOnline =
      _connectivityService.NetworkAvailability is NetworkAvailability.Internet or NetworkAvailability.PartialInternet;
    if (_currentlyOnline)
      _onlineTcs.SetResult();

    _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
  }

  public void Dispose()
  {
    _httpClient.Dispose();
    _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

    GC.SuppressFinalize(this);
  }

  private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityChangedEventArgs e)
  {
    var isNowOnline = e.NetworkAvailability is NetworkAvailability.Internet or NetworkAvailability.PartialInternet;
    lock (_onlineStateLock)
    {
      if (_currentlyOnline == isNowOnline) return;
      _currentlyOnline = isNowOnline;
      if (isNowOnline)
        _onlineTcs.TrySetResult();
      else
        _onlineTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
  }

  private Task WaitUntilOnlineAsync()
  {
    lock (_onlineStateLock)
    {
      return _onlineTcs.Task;
    }
  }

  public async Task<MinimalMessageState?> SendWebhook(Message webhook)
  {
    var uri = new UriBuilder(_httpClient.BaseAddress!.ToString().TrimEnd('/')) { Query = "wait=true" }.Uri;

    try
    {
      await WaitUntilOnlineAsync().ConfigureAwait(false);
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

  private async Task PatchWebhook(ulong messageId, Message webhook)
  {
    var uri = new Uri($"messages/{messageId}", UriKind.Relative);

    try
    {
      await WaitUntilOnlineAsync().ConfigureAwait(false);
      _ = await _httpClient.PatchAsJsonAsync(uri, webhook, SerializerOptions).ConfigureAwait(false);
    }
    catch
    {
      LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(messageId);
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
    }
  }

  public async Task PatchWebhook(ulong messageId, Message webhook, Stream? imageStream)
  {
    if (imageStream is null)
    {
      await PatchWebhook(messageId, webhook).ConfigureAwait(false);
      return;
    }

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
      await WaitUntilOnlineAsync().ConfigureAwait(false);
      _ = await _httpClient.PatchAsync(uri, form).ConfigureAwait(false);
    }
    catch
    {
      LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(messageId);
      LogAnExceptionOccuredInTheWebhookdumpPlugin();
    }
  }

  public async Task<MinimalMessageState?> GetWebhookMessageState(ulong messageId)
  {
    var uri = new Uri($"messages/{messageId}", UriKind.Relative);
    // TODO: Take advantage of the Shoko connectivity checks to determine if we are connected to the internet

    try
    {
      await WaitUntilOnlineAsync().ConfigureAwait(false);
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
  partial void LogExceptionThrownWhenPatchingAPreviousWebhookMessageMessageidId(ulong id);

  [LoggerMessage(LogLevel.Error, "Unable to retreive discord message status (MessageId={id})")]
  partial void LogUnableToRetreiveDiscordMessageStatusMessageidId(ulong id);
}
