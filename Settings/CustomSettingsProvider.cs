using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Reflection;

namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettingsProvider : ICustomSettingsProvider
{
	private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
	private static readonly object SettingsLock = new();
	private const string SettingsFilename = "WebhookDump.json";
	private readonly string SettingsPath = Path.Combine(ApplicationPath, SettingsFilename);
	private static ICustomSettings Instance { get; set; }

	public CustomSettingsProvider()
	{
		LoadSettings();
	}

	public ICustomSettings GetSettings()
	{
		if (Instance == null) LoadSettings();
		return Instance;
	}

	public void SaveSettings(ICustomSettings settings)
	{
		Instance = settings;
		SaveSettings();
	}

	public void SaveSettings()
	{
		var context = new ValidationContext(Instance, null, null);
		var results = new List<ValidationResult>();

		if (!Validator.TryValidateObject(Instance, context, results))
		{
				results.ForEach(s => Logger.Error(s.ErrorMessage));
				throw new ValidationException();
		}

		lock (SettingsLock)
		{
			var settingsStored = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : string.Empty;
			var settingsMemory = JsonSerializer.Serialize(Instance);

			if (!settingsStored.Equals(settingsMemory, StringComparison.Ordinal))
			{
				File.WriteAllText(SettingsPath, settingsMemory);
			}
		}
	}

	private void LoadSettings()
	{
		if (!File.Exists(SettingsPath))
		{
			Instance = new CustomSettings();
			SaveSettings();
			return;
		}

		LoadSettingsFromFile(SettingsPath);
		SaveSettings();
	}

	private static void LoadSettingsFromFile(string path)
	{
		try
		{
			Instance = JsonSerializer.Deserialize<CustomSettings>(File.ReadAllText(path));
		}
		catch (Exception e)
		{
			Logger.Error("Couldn't load plugin settings from file!", e);
		}
	}

	#region `ShokoServer/Shoko.Server/Utilities/Utils.cs` - no way of accessing via API :(
	private static bool IsLinux
	{
		get
		{
			var p = (int)Environment.OSVersion.Platform;
			return p == 4 || p == 6 || p == 128;
		}
	}

	private static string DefaultInstance { get; set; } = Assembly.GetEntryAssembly().GetName().Name;

	private static string ApplicationPath
	{
		get
		{
			if (IsLinux)
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko",
						DefaultInstance);
			}

			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
					DefaultInstance);
		}
	}

	#endregion
}