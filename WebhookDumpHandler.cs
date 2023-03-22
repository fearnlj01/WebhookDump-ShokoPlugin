using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shoko.Plugin.WebhookDump
{
	public class WebhookDumpHandler : IPlugin
	{
		private static readonly HttpClient httpClient = new();

		public string Name => "WebhookDump";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly string WebhookUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_URL");

		private static readonly string AvatarUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_AVATAR_URL");

		private static readonly string DiscordShokoUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_SHOKO_URL");

		private static readonly string ApiKey = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_APIKEY");

		public WebhookDumpHandler(IShokoEventHandler eventHandler)
		{
			eventHandler.FileNotMatched += OnFileNotMatched;
		}

		public void OnSettingsLoaded(IPluginSettings settings)
		{
		}

		public void Load()
		{
		}

		private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
		{
			var fileInfo = fileNotMatchedEvent.FileInfo;
			if (fileNotMatchedEvent.AutoMatchAttempts == 1 && IsProbablyAnime(fileInfo))
			{
				var result = await DumpFile(fileInfo);
				if (WebhookUrl != null)
				{
					var json = JsonSerializer.Serialize(GetWebhook(fileInfo, result));
					HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
					{
						Content = new StringContent(json, Encoding.UTF8, "application/json")
					};
					try
					{
						var response = await httpClient.SendAsync(request);

						response.EnsureSuccessStatusCode();
					}
					catch (HttpRequestException e)
					{
						Logger.Error("Webhook failed to send!", e);
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
			HttpRequestMessage request = new(HttpMethod.Post, $"http://127.0.0.1:8111/api/v3/File/{file.VideoFileID}/AVDump")
			{
				Headers = {
					{"accept", "*/*"},
					{"apikey", ApiKey }
				}
			};

			try
			{
				var response = await httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				var content = await response.Content.ReadAsStringAsync();
				return JsonSerializer.Deserialize<AVDumpResult>(content);
			}
			catch (HttpRequestException e)
			{
				Logger.Error("Error automatically AVDumping file", e);
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