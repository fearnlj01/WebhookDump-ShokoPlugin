using System.Text;
using System.Text.Json;

namespace Shoko.Plugin.WebhookDump.Misc;
public class WebhookNamingPolicy : JsonNamingPolicy
{
  public override string ConvertName(string name)
  {
    StringBuilder builder = new();

    for (var i = 0; i < name.Length; i++)
    {
      if (i > 0 && char.IsUpper(name[i]))
      {
        _ = builder.Append('_');
      }
      _ = builder.Append(char.ToLowerInvariant(name[i]));
    }

    return builder.ToString();
  }
}
