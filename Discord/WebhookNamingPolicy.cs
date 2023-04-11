using System.Text;
using System.Text.Json;

namespace Shoko.Plugin.WebhookDump;
public class WebhookNamingPolicy : JsonNamingPolicy
{
  public override string ConvertName(string name)
  {
    var builder = new StringBuilder();

    for (int i = 0; i < name.Length; i++)
    {
      if (i > 0 && char.IsUpper(name[i]))
      {
        builder.Append('_');
      }
      builder.Append(char.ToLowerInvariant(name[i]));
    }

    return builder.ToString();
  }
}
