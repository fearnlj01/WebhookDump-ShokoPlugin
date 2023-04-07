namespace Shoko.Plugin.WebhookDump.Settings;

public interface IShokoSettings
{
  string ApiKey { get; set; }
  int ServerPort { get; set; }
  string PublicUrl { get; set; }
  int? PublicPort { get; set; }
  AutomaticMatchSettings AutomaticMatch { get; set; }
}