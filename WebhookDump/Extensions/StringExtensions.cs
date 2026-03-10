using System.Text.RegularExpressions;

namespace Shoko.Plugin.WebhookDump.Extensions;

public static partial class StringExtensions
{
  [GeneratedRegex(@"^(?:\[[^\]]+\]\s*)?" + // Skip Group tags
                  "(?<SeriesName>.+?)" + // Lazily captures the series title until the following 'break' criteria is matched
                  "(?:" +
                  @"\s+-\s+(?=\d|[Ss]\d{2}[Ee]\d{2,4})" + // " - 0" || " - S01E01"
                  "|" +
                  @"(?!.*\s+-\s+(?=\d))[\. ](?:19|20)\d{2}" + // "2026" | "1999" (But not if " - " is found straight AFTER the year)
                  "|" +
                  @"[\. ][Ss]\d{2}[Ee]\d{2,4}" + // " S01E01" | ".S01E9999"
                  "|" +
                  @"\.\w{3,4}$" + // ".mkv"
                  "|$)", // Falling back to the end of the string.
    RegexOptions.Compiled)]
  private static partial Regex TitleRegex();

  [GeneratedRegex("^#?(?<Colour>(?:[0-9a-f]{3}){1,2})$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase)]
  private static partial Regex HexadecimalColourRegex();

  [GeneratedRegex(@"(?<MarkdownCharacter>[*_~|])(?<RegularText>.+?)\1", RegexOptions.Compiled)]
  private static partial Regex MarkdownPairsRegex();

  extension(string? input)
  {
    public string? GetHexadecimalColour()
    {
      if (string.IsNullOrWhiteSpace(input)) return null;

      var match = HexadecimalColourRegex().Match(input);
      if (!match.Success) return null;

      var value = match.Groups["Colour"].Value.ToLowerInvariant();
      return value.Length == 3
        ? string.Concat(value.Select(c => $"{c}{c}"))
        : value;
    }

    public string ExtractFileTitle()
    {
      if (string.IsNullOrWhiteSpace(input)) return string.Empty;
      var match = TitleRegex().Match(input);
      return match.Success ? match.Groups["SeriesName"].Value.Replace('.', ' ') : string.Empty;
    }

    public string EscapeMarkdownPairs()
    {
      return input is null
        ? string.Empty
        : MarkdownPairsRegex().Replace(input, match =>
        {
          var mChar = match.Groups["MarkdownCharacter"].Value;
          var content = match.Groups["RegularText"].Value;

          return $@"\{mChar}{content}\{mChar}";
        });
    }
  }
}
