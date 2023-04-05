using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Plugin.WebhookDump.Settings;

public class ShokoSettings : IShokoSettings
{
	[DefaultValue(null)]
  public string ApiKey { get; set; }

	[Required]
	[Range(1, 65536, ErrorMessage = "A server port of no more than 65536 may be set.")]
	[DefaultValue(8111)]
  public int ServerPort { get; set; }

	[DefaultValue("http://localhost")]
  public string PublicUrl { get; set; }

	[DefaultValue(null)]
  public int? PublicPort { get; set; }
}