namespace Shoko.Plugin.WebhookDump.Settings;

public interface IShokoSettings
{
  public string ApiKey { get; }
  public int ServerPort { get; }
  public string PublicUrl { get; }
  public int? PublicPort { get; }
}