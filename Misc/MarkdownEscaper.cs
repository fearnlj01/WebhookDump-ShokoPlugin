using System.Text.RegularExpressions;

namespace Shoko.Plugin.WebhookDump.Misc;

public static partial class MarkdownEscaper
{
  [GeneratedRegex(@"(\*|_|~|\|)(.+?)\1", RegexOptions.Compiled)]
  private static partial Regex MarkdownPairsRegex();

  public static string EscapeMarkdownPairs(string input) => MarkdownPairsRegex().Replace(input, match =>
  {
    var markdownCharacter = match.Groups[1].Value;
    var content = match.Groups[2].Value;

    return $@"\{markdownCharacter}{content}\{markdownCharacter}";
  });
}
