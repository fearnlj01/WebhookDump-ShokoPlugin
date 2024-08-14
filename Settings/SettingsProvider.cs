using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using System.Text.Json;
using NLog;

namespace Shoko.Plugin.WebhookDump.Settings;
public class SettingsProvider : ISettingsProvider
{
  private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
  private readonly string _filePath = Path.Combine(ApplicationPath, "WebhookDump.json");
  private readonly object _settingsLock = new();
  private CustomSettings _settings;

  #region `ShokoServer/Shoko.Server/Utilities/Utils.cs`
  private static bool IsLinux => Environment.OSVersion.Platform is PlatformID.Unix;

  private static string DefaultInstance => Assembly.GetEntryAssembly().GetName().Name;

  private static string ApplicationPath => IsLinux
          ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko",
              DefaultInstance)
          : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          DefaultInstance);
  #endregion

  private static readonly JsonSerializerOptions _options = new()
  {
    AllowTrailingCommas = true,
    WriteIndented = true
  };

  public ISettings GetSettings()
  {
    _settings ??= GetSettingsFromFile();

    return _settings;
  }

  public void SaveSettings(ISettings settings)
  {
    ValidateSettings(settings);

    string json = JsonSerializer.Serialize(settings, _options);

    lock (_settingsLock)
    {
      using FileStream stream = new(_filePath, FileMode.Create);
      using StreamWriter writer = new(stream);
      writer.Write(json);
    }
  }

  private CustomSettings GetSettingsFromFile()
  {
    CustomSettings settings;
    try
    {
      string contents = File.ReadAllText(_filePath);
      settings = JsonSerializer.Deserialize<CustomSettings>(contents, _options);
    }
    catch (FileNotFoundException)
    {
      settings = new CustomSettings();
    }

    ValidateSettings(settings);
    SaveSettings(settings); // Saves too often... But makes future additions easier to edit

    return settings;
  }

  private static void ValidateSettings(ISettings settings)
  {
    List<ValidationResult> validationResults = new();
    ValidationContext validationContext = new(settings);

    bool isValid = Validator.TryValidateObject(
      settings, validationContext, validationResults, true
    );

    if (!isValid)
    {
      foreach (ValidationResult validationResult in validationResults)
      {
        foreach (string memberName in validationResult.MemberNames)
        {
          _logger.Error($"Error validating settings for property {memberName} : {validationResult.ErrorMessage}");
        }
      }
      throw new ArgumentException("Error in settings validation");
    }
  }
}
