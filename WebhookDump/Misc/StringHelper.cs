using System.Net;
using System.Text.RegularExpressions;

namespace Shoko.Plugin.WebhookDump.Misc;

public static partial class StringHelper
{
  [GeneratedRegex(@"^((\[.*?\]\s*)*)(?<SeriesName>.+(?= - ))(.*)$", RegexOptions.Compiled)]
  private static partial Regex TitleRegex();

  [GeneratedRegex("^#?(?<Colour>(?:[0-9a-f]{3}){1,2})$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase)]
  private static partial Regex HexadecimalColourRegex();

  [GeneratedRegex(@"(?<MarkdownCharacter>[*_~|])(?<RegularText>.+?)\1", RegexOptions.Compiled)]
  private static partial Regex MarkdownPairsRegex();

  public static string EscapeMarkdownPairs(string input)
  {
    return MarkdownPairsRegex().Replace(input, match =>
    {
      var mChar = match.Groups["MarkdownCharacter"].Value;
      var content = match.Groups["RegularText"].Value;

      return $@"\{mChar}{content}\{mChar}";
    });
  }

  public static string? GetHexadecimalColour(string input)
  {
    var match = HexadecimalColourRegex().Match(input);
    if (!match.Success) return null;

    var value = match.Groups["Colour"].Value.ToLowerInvariant();
    return value.Length == 3
      ? string.Concat(value.Select(c => $"{c}{c}"))
      : value;
  }

  public static string ExtractTitle(string input)
  {
    return TitleRegex().Replace(input, match => WebUtility.UrlEncode(match.Groups["SeriesName"].Value));
  }
}
