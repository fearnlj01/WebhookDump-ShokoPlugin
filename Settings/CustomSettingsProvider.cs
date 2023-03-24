using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Shoko.Plugin.WebhookDump.Settings;

public class CustomSettingsProvider : ICustomSettingsProvider
{
	private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
	private readonly string _filePath = Path.Combine(ApplicationPath, "WebhookDump.json");
	private readonly object _settingsLock = new();
	private CustomSettings _settings;

	private readonly static JsonSerializerOptions options = new()
	{
		AllowTrailingCommas = true,
		WriteIndented = true
	};

	public CustomSettings GetSettings()
	{
		_settings ??= GetSettingsFromFile();

		return _settings;
	}

	public void SaveSettings(CustomSettings settings)
	{
		ValidateSettings(settings);

		var json = JsonSerializer.Serialize(settings, options);

		lock (_settingsLock)
		{
			using FileStream stream = new(_filePath, FileMode.Create);
			using StreamWriter writer = new(stream);
			writer.Write(json);
		}
	}

	private static void ValidateSettings(CustomSettings settings)
	{
		var validationResults = new List<ValidationResult>();
		var validationContext = new ValidationContext(settings);

		bool isValid = Validator.TryValidateObject(
			settings, validationContext, validationResults, true);

		if (!isValid)
		{
			var errorMessages = new List<string>();
			foreach (var validationResult in validationResults)
			{
				errorMessages.Add(validationResult.ErrorMessage);
				foreach (var memberName in validationResult.MemberNames)
				{
					_logger.Error($"Error in settings validation: ${memberName}");
				}
			}
			var errString = string.Join(Environment.NewLine, errorMessages);
			_logger.Error($"Error in settings validation - Error Messages: '{errString}'");
			throw new ArgumentException(errString);
		}
	}

	private CustomSettings GetSettingsFromFile()
	{
		CustomSettings settings = null;
		try
		{
			using FileStream stream = new(_filePath, FileMode.Open);
			settings = JsonSerializer.Deserialize<CustomSettings>(stream, options);
		}
		catch (FileNotFoundException)
		{
			settings = new CustomSettings();
			SaveSettings(settings);
		}

		ValidateSettings(settings);

		return settings;
	}

	#region `ShokoServer/Shoko.Server/Utilities/Utils.cs`
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