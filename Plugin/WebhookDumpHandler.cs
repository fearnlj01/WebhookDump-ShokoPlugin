using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.WebhookDump.Utils;
using Shoko.Server;
using Shoko.Server.API.v3.Models.Common;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shoko.Plugin.WebhookDump
{
	public class WebhookDumpHandler : IPlugin
	{
		private static readonly HttpClient httpClient = new();
		public string Name => "WebhookDump";
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly string WebhookUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_URL");
		private static readonly string WebhookAvatarUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_AVATAR_URL");
		// e.g. https://shoko.publicdomain.co.uk or http://10.0.0.100:8111
		private static readonly string WebhookShokoUrl = Environment.GetEnvironmentVariable("SHOKO_DISCORD_WEBHOOK_SHOKO_URL");


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

		private void OnFileNotMatched(object sender, FileNotMatchedEventArgs fileNotMatchedEvent)
		{
			if (fileNotMatchedEvent.AutoMatchAttempts == 1 && IsProbablyAnime(fileNotMatchedEvent.FileInfo))
			{
				var avdumpOutput = AVDumpHelper.DumpFile(fileNotMatchedEvent.FileInfo.FilePath).Replace("\r", "");
				var result = new AVDumpResult
				{
					FullOutput = avdumpOutput,
					Ed2k = avdumpOutput.Split("\n").FirstOrDefault(s => s.Trim().Contains("ed2k://"))
				};
				Logger.Info($"ED2K:  \"{result.Ed2k}\"");
				if (WebhookShokoUrl != null)
				{
					var json = JsonSerializer.Serialize(GetWebhook(fileNotMatchedEvent.FileInfo, result));
					HttpRequestMessage request = new(HttpMethod.Post, WebhookUrl)
					{
						Content = new StringContent(json, Encoding.UTF8, "application/json")
					};
					httpClient.Send(request);
				}
			}
		}

		private static Webhook GetWebhook(IVideoFile file, AVDumpResult result)
		{
			return new Webhook()
			{
				username = "Shoko",
				avatar_url = WebhookAvatarUrl,
				content = null,
				embeds = new List<IWebhookEmbed>
				{
					new WebhookEmbed
					{
						title = file.Filename,
						description = "The above file has been found by Shoko Server but could not be matched against AniDB. The file has now been dumped with AVDump, result as below.",
						url = WebhookShokoUrl + "/webui/utilities/unrecognized/files",
						color = 0x3B82F6,
						fields = new List<IWebhookField>
						{
							new WebhookField
							{
								name = "ED2K:",
								value = result.Ed2k
							}
						}
					}
				}
			};
		}

		private static bool IsProbablyAnime(IVideoFile file)
		{
			// TODO: There's a lot more regex checks that can probably be done here...
			//       Hopefully this is enough to filter out the worst of it at least
			var regex = new Regex(@"^(\[[^]]+\]).+\.mkv$");
			return file.FileSize > 100_000_000 && regex.IsMatch(file.Filename);
		}
	}
}