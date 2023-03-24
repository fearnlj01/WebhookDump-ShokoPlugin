using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using Shoko.Plugin.WebhookDump.Settings;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shoko.Plugin.WebhookDump
{
	public class WebhookDump : IPlugin
	{
		private static readonly HttpClient _httpClient = new();

		public string Name => "WebhookDump";

		private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		private readonly CustomSettingsProvider _settingsProvider;

		private readonly CustomSettings _settings;

		#region EnvVars
		private static readonly string WebhookUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_URL");

		private static readonly string AvatarUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_AVATAR_URL");

		private static readonly string DiscordShokoUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_SHOKO_URL");

		private static readonly string ServerPort = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_SHOKO_PORT") ?? "8111";

		private static readonly string ApiKey = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_APIKEY");
		#endregion

		public static void ConfigureServices(IServiceCollection services)
		{
			services.AddSingleton<CustomSettingsProvider>();
			services.AddScoped<IPlugin, WebhookDump>();
		}
		public WebhookDump(IShokoEventHandler eventHandler, CustomSettingsProvider settingsProvider)
		{
			eventHandler.FileNotMatched += OnFileNotMatched;
			_settingsProvider = settingsProvider;
			_settings = _settingsProvider.GetSettings();
		}

		public void OnSettingsLoaded(IPluginSettings settings)
		{
		}

		public void Load()
		{
			var settingsProvider = new Settings.CustomSettingsProvider();
			var settings = settingsProvider.GetSettings();
		}

		private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
		{
			var fileInfo = fileNotMatchedEvent.FileInfo;
			if (fileNotMatchedEvent.AutoMatchAttempts == 1 && IsProbablyAnime(fileInfo))
			{
				var result = await DumpFile(fileInfo);
				if (WebhookUrl != null)
				{
					JsonSerializerOptions options = new()
					{
						PropertyNamingPolicy = new WebhookNamingPolicy()
					};
					var json = JsonSerializer.Serialize(GetWebhook(fileInfo, result), options);
					HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
					{
						Content = new StringContent(json, Encoding.UTF8, "application/json")
					};
					try
					{
						var response = await _httpClient.SendAsync(request);

						response.EnsureSuccessStatusCode();
					}
					catch (HttpRequestException e)
					{
						_logger.Error("Webhook failed to send!", e);
					}
				}
			}
		}

		private static Webhook GetWebhook(IVideoFile file, AVDumpResult result)
		{
			return new Webhook()
			{
				Username = "Shoko",
				AvatarUrl = AvatarUrl,
				Content = null,
				Embeds = new WebhookEmbed[]
				{
					new WebhookEmbed
					{
						Title = file.Filename,
						Description = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
						Url = DiscordShokoUrl + "/webui/utilities/unrecognized/files",
						Color = 0x3B82F6,
						Fields = new WebhookField[]
						{
							new WebhookField() {
								Name = "ED2K:",
								Value = result.Ed2k
							}
						}
					}
				}
			};
		}

		private static async Task<AVDumpResult> DumpFile(IVideoFile file)
		{
			HttpRequestMessage request = new(HttpMethod.Post, $"http://localhost:{ServerPort}/api/v3/File/{file.VideoFileID}/AVDump")
			{
				Headers = {
					{"accept", "*/*"},
					{"apikey", ApiKey }
				}
			};

			try
			{
				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				var content = await response.Content.ReadAsStringAsync();
				return JsonSerializer.Deserialize<AVDumpResult>(content);
			}
			catch (HttpRequestException e)
			{
				_logger.Error("Error automatically AVDumping file", e);
				throw;
			}
		}

		private static bool IsProbablyAnime(IVideoFile file)
		{
			// TODO: There's a lot more regex checks that can probably be done here...
			//       Hopefully this is enough to filter out the worst of it at least
			var regex = new Regex(@"^(\[[^]]+\]).+\.mkv$");
			return file.FileSize > 100_000_000
				&& regex.IsMatch(file.Filename);
		}
	}
}