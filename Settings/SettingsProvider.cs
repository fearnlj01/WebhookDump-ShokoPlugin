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

  private readonly static JsonSerializerOptions _options = new()
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

    var json = JsonSerializer.Serialize(settings, _options);

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
      var contents = File.ReadAllText(_filePath);
      settings = JsonSerializer.Deserialize<CustomSettings>(contents, _options);
    }
    catch (FileNotFoundException)
    {
      settings = new CustomSettings();
      SaveSettings(settings);
    }

    ValidateSettings(settings);

    return settings;
  }

  private static void ValidateSettings(ISettings settings)
  {
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(settings);

    bool isValid = Validator.TryValidateObject(
      settings, validationContext, validationResults, true
    );

    if (!isValid)
    {
      foreach (var validationResult in validationResults)
      {
        foreach (var memberName in validationResult.MemberNames)
        {
          _logger.Error($"Error validating settings for property {memberName} : {validationResult.ErrorMessage}");
        }
      }
      throw new ArgumentException("Error in settings validation");
    }
  }
}
