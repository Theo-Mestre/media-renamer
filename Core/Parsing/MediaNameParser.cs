using System.Text.RegularExpressions;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Parsing;

public static partial class MediaNameParser
{
    [GeneratedRegex(@"^(?<prefix>.+?)\s*-\s*(?<num>\d{2,})$")]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex(@"[\s_\-]+(?<num>\d+)$")]
    public static partial Regex LooseTrailingNumberRegex();

    public static ParsedMediaName Parse(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);

        int? index = null;
        int padding = 3;
        string withoutNumber = baseName;

        var match = TrailingNumberRegex().Match(baseName);
        if (match.Success)
        {
            withoutNumber = match.Groups["prefix"].Value;
            string numStr = match.Groups["num"].Value;
            index = int.Parse(numStr);
            padding = Math.Max(3, numStr.Length);
        }

        string[] segments = withoutNumber.Split(" - ", StringSplitOptions.TrimEntries);

        string namePart;
        string artistPart;

        if (segments.Length >= 2)
        {
            artistPart = segments[^1];
            namePart = string.Join(" - ", segments[..^1]);
        }
        else
        {
            namePart = withoutNumber.Trim();
            artistPart = string.Empty;
        }

        return new ParsedMediaName(baseName, ext, namePart, artistPart, index, padding);
    }
}