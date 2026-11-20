using System.Text.RegularExpressions;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Parsing;

public static partial class MediaNameParser
{
    [GeneratedRegex(@"^(?<stem>.+?)\s+-\s+(?<num>\d{2,})$", RegexOptions.Compiled)]
    public static partial Regex TrailingIndexRegex();

    [GeneratedRegex(@"[\s_\-]+(?<num>\d+)$", RegexOptions.Compiled)]
    public static partial Regex LooseTrailingNumberRegex();

    public static ParsedMediaName Parse(string fileName)
    {
        string ext = Path.GetExtension(fileName);
        string rawBase = Path.GetFileNameWithoutExtension(fileName);

        int? indexNumber = null;
        int paddingWidth = 3;
        string stemWithoutIndex = rawBase.Trim();

        var indexMatch = TrailingIndexRegex().Match(stemWithoutIndex);
        if (indexMatch.Success)
        {
            string numStr = indexMatch.Groups["num"].Value;
            indexNumber = int.Parse(numStr);
            paddingWidth = Math.Max(3, numStr.Length);
            stemWithoutIndex = indexMatch.Groups["stem"].Value.Trim();
        }

        string[] segments = stemWithoutIndex
            .Split(" - ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string namePart;
        string characterPart;
        string artistPart;

        if (segments.Length >= 3)
        {
            // License - Character - Artist
            artistPart = segments[^1];
            characterPart = segments[^2];
            namePart = string.Join(" - ", segments[..^1]); // "License - Character"
        }
        else if (segments.Length == 2)
        {
            // Character - Artist
            artistPart = segments[1];
            characterPart = segments[0];
            namePart = segments[0]; // "Character"
        }
        else
        {
            artistPart = string.Empty;
            characterPart = segments.Length == 1 ? segments[0] : stemWithoutIndex;
            namePart = characterPart;
        }

        return new ParsedMediaName(
            rawBase,
            ext,
            namePart,
            characterPart,
            artistPart,
            indexNumber,
            paddingWidth);
    }
}