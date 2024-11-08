using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using NLog;

namespace Shoko.Plugin.WebhookDump.Settings;
public class SettingsProvider
{
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
  private readonly string _filePath = Path.Combine(ApplicationPath, "WebhookDump.json");
  private readonly object _settingsLock = new();
  private CustomSettings _settings;

  #region `ShokoServer/Shoko.Server/Utilities/Utils.cs`
  private static bool IsLinux => Environment.OSVersion.Platform is PlatformID.Unix;

  private static string DefaultInstance => Assembly.GetEntryAssembly()?.GetName().Name;

  private static string ApplicationPath => IsLinux
          ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko",
              DefaultInstance)
          : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          DefaultInstance);
  #endregion

  private static readonly JsonSerializerOptions Options = new()
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

    var json = JsonSerializer.Serialize(settings, Options);

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
      settings = JsonSerializer.Deserialize<CustomSettings>(contents, Options);
    }
    catch (FileNotFoundException)
    {
      settings = new CustomSettings();
    }

    ValidateSettings(settings);
    SaveSettings(settings); // Saves too often... But makes future additions easier to edit

    return settings;
  }

  private static void ValidateSettings(CustomSettings settings)
  {
    List<ValidationResult> validationResults = [];
    ValidationContext validationContext = new(settings);

    var isValid = Validator.TryValidateObject(
      settings, validationContext, validationResults, true
    );

    if (!isValid)
    {
      foreach (var validationResult in validationResults)
      {
        foreach (var memberName in validationResult.MemberNames)
        {
          Logger.Error($"Error validating settings for property {memberName} : {validationResult.ErrorMessage}");
        }
      }
      throw new ArgumentException("Error in settings validation");
    }
  }
}
