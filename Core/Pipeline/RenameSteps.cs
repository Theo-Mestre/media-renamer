using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MediaRenamer.Core.Models;
using MediaRenamer.Core.Parsing;

namespace MediaRenamer.Core.Pipeline;

public sealed class StripLegacyNumberStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        if (!context.Config.StripLegacyNumbers)
            return currentBaseName;

        var standardMatch = MediaNameParser.Parse(currentBaseName + ".tmp");
        if (standardMatch.IndexNumber.HasValue)
        {
            context.PreservedIndex = standardMatch.IndexNumber;
            context.PreservedPadding = standardMatch.PaddingWidth;
            return standardMatch.IdentityPrefix;
        }

        var looseMatch = MediaNameParser.LooseTrailingNumberRegex().Match(currentBaseName);
        if (looseMatch.Success)
        {
            context.PreservedIndex = int.Parse(looseMatch.Groups["num"].Value);
            return currentBaseName[..looseMatch.Index].Trim();
        }

        return currentBaseName;
    }
}

public sealed class SplitNameArtistStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        if (currentBaseName.Contains(" - ", StringComparison.Ordinal))
            return currentBaseName;

        if (!string.IsNullOrWhiteSpace(context.Config.ArtistDelimiterHint))
        {
            string artist = context.Config.ArtistDelimiterHint.Trim();
            int idx = currentBaseName.LastIndexOf(artist, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                string namePart = currentBaseName[..idx].Trim();
                string artistPart = currentBaseName[idx..].Trim();
                return $"{namePart} - {artistPart}";
            }
        }
        else if (context.Config.SplitNameAfterWords is > 0)
        {
            string[] words = currentBaseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > context.Config.SplitNameAfterWords.Value)
            {
                string namePart = string.Join(" ", words[..context.Config.SplitNameAfterWords.Value]);
                string artistPart = string.Join(" ", words[context.Config.SplitNameAfterWords.Value..]);
                return $"{namePart} - {artistPart}";
            }
        }

        return currentBaseName;
    }
}

public sealed class SetBaseIdentityStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        return string.IsNullOrWhiteSpace(context.Config.SetBaseIdentity)
            ? currentBaseName
            : context.Config.SetBaseIdentity.Trim();
    }
}

public sealed class FindReplaceStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        if (string.IsNullOrEmpty(context.Config.FindText))
            return currentBaseName;

        return currentBaseName.Replace(
            context.Config.FindText,
            context.Config.ReplaceText,
            StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AffixStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        var parsed = MediaNameParser.Parse(currentBaseName + ".tmp");
        string identity = parsed.IdentityPrefix;

        if (!string.IsNullOrEmpty(context.Config.Prefix))
            identity = context.Config.Prefix + identity;

        if (!string.IsNullOrEmpty(context.Config.Suffix))
            identity += context.Config.Suffix;

        return identity;
    }
}

public sealed class CaseTransformStep : IRenameStep
{
    public string Execute(string currentBaseName, RenameContext context)
    {
        string result = currentBaseName;

        if (context.Config.CaseMode != CaseTransform.None)
        {
            string[] segments = result.Split(" - ", StringSplitOptions.TrimEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = ConvertCase(segments[i], context.Config.CaseMode);
            }
            result = string.Join(" - ", segments);
        }

        if (!context.Config.AutoCount && context.PreservedIndex.HasValue)
        {
            return $"{result} - {context.PreservedIndex.Value.ToString($"D{context.PreservedPadding}")}";
        }

        return result;
    }

    private static string ConvertCase(string input, CaseTransform mode)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        return mode switch
        {
            CaseTransform.TitleCase => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant()),
            CaseTransform.PascalCase => ToCamelOrPascal(input, upperFirst: true),
            CaseTransform.CamelCase => ToCamelOrPascal(input, upperFirst: false),
            _ => input
        };
    }

    private static string ToCamelOrPascal(string input, bool upperFirst)
    {
        var words = Regex.Split(input, @"[\s_\-]+").Where(w => w.Length > 0).ToArray();
        if (words.Length == 0) return input;

        var sb = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            if (i == 0 && !upperFirst)
            {
                sb.Append(char.ToLowerInvariant(w[0]));
                if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
            }
            else
            {
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
            }
        }
        return sb.ToString();
    }
}