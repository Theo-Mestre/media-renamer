using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MediaRenamer.Core.Models;
using MediaRenamer.Core.Parsing;

namespace MediaRenamer.Core.Pipeline;

public sealed class RenameContext
{
    public int? PreservedIndex { get; set; }
    public int PreservedPadding { get; set; } = 3;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$op")]
[JsonDerivedType(typeof(ReplaceStringOperation), "replace")]
[JsonDerivedType(typeof(SetBaseIdentityOperation), "setIdentity")]
[JsonDerivedType(typeof(AddAffixOperation), "affix")]
[JsonDerivedType(typeof(StripLegacyNumberOperation), "stripNumbers")]
[JsonDerivedType(typeof(SplitNameArtistOperation), "splitArtist")]
[JsonDerivedType(typeof(CaseTransformOperation), "case")]
[JsonDerivedType(typeof(TrimCharactersOperation), "trimChars")]
public abstract class RenameOperation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..6];
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public abstract string DisplayTitle { get; }

    [JsonIgnore]
    public abstract string Summary { get; }

    public abstract string Apply(string currentBaseName, RenameContext context);
}

public sealed class ReplaceStringOperation : RenameOperation
{
    public string SearchFor { get; set; } = string.Empty;
    public string ReplaceWith { get; set; } = string.Empty;
    public bool UseRegex { get; set; } = false;
    public bool CaseSensitive { get; set; } = false;

    public override string DisplayTitle => "Replace String";

    public override string Summary
    {
        get
        {
            if (string.IsNullOrEmpty(SearchFor))
                return "(empty search)";

            string flags = (UseRegex, CaseSensitive) switch
            {
                (true, true) => " [Regex, Case]",
                (true, false) => " [Regex]",
                (false, true) => " [Case]",
                _ => string.Empty
            };
            return $"'{SearchFor}' → '{ReplaceWith}'{flags}";
        }
    }

    public override string Apply(string currentBaseName, RenameContext context)
    {
        if (string.IsNullOrEmpty(SearchFor))
            return currentBaseName;

        if (UseRegex)
        {
            try
            {
                var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.Replace(currentBaseName, SearchFor, ReplaceWith, options);
            }
            catch
            {
                return currentBaseName;
            }
        }

        var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return currentBaseName.Replace(SearchFor, ReplaceWith, comparison);
    }
}

public sealed class SetBaseIdentityOperation : RenameOperation
{
    public string NewIdentity { get; set; } = string.Empty;

    public override string DisplayTitle => "Set Base Identity";
    public override string Summary => string.IsNullOrWhiteSpace(NewIdentity)
        ? "(not set)"
        : $"'{NewIdentity}'";

    public override string Apply(string currentBaseName, RenameContext context)
    {
        return string.IsNullOrWhiteSpace(NewIdentity)
            ? currentBaseName
            : NewIdentity.Trim();
    }
}

public sealed class AddAffixOperation : RenameOperation
{
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;

    public override string DisplayTitle => "Add Prefix / Suffix";
    public override string Summary => $"Prefix: '{Prefix}' | Suffix: '{Suffix}'";

    public override string Apply(string currentBaseName, RenameContext context)
    {
        var parsed = MediaNameParser.Parse(currentBaseName + ".tmp");
        string identity = parsed.IdentityPrefix;

        if (!string.IsNullOrEmpty(Prefix))
            identity = Prefix + identity;

        if (!string.IsNullOrEmpty(Suffix))
            identity += Suffix;

        return identity;
    }
}

public sealed class StripLegacyNumberOperation : RenameOperation
{
    public override string DisplayTitle => "Strip Legacy Numbers";
    public override string Summary => "Removes trailing ' 00', '_01', '-002' (saves index if Auto-Count is off)";

    public override string Apply(string currentBaseName, RenameContext context)
    {
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

public sealed class SplitNameArtistOperation : RenameOperation
{
    public int? SplitAfterWords { get; set; } = 1;
    public string? ArtistHint { get; set; }

    public override string DisplayTitle => "Split Name / Artist";
    public override string Summary => !string.IsNullOrWhiteSpace(ArtistHint)
        ? $"Before artist '{ArtistHint}'"
        : $"After {SplitAfterWords ?? 1} word(s)";

    public override string Apply(string currentBaseName, RenameContext context)
    {
        if (currentBaseName.Contains(" - ", StringComparison.Ordinal))
            return currentBaseName;

        if (!string.IsNullOrWhiteSpace(ArtistHint))
        {
            string artist = ArtistHint.Trim();
            int idx = currentBaseName.LastIndexOf(artist, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                string namePart = currentBaseName[..idx].Trim();
                string artistPart = currentBaseName[idx..].Trim();
                return $"{namePart} - {artistPart}";
            }
        }
        else if (SplitAfterWords is > 0)
        {
            string[] words = currentBaseName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > SplitAfterWords.Value)
            {
                string namePart = string.Join(" ", words[..SplitAfterWords.Value]);
                string artistPart = string.Join(" ", words[SplitAfterWords.Value..]);
                return $"{namePart} - {artistPart}";
            }
        }

        return currentBaseName;
    }
}

public sealed class TrimCharactersOperation : RenameOperation
{
    public int TrimStartCount { get; set; } = 0;
    public int TrimEndCount { get; set; } = 0;

    public override string DisplayTitle => "Trim Characters";
    public override string Summary => $"Start: -{TrimStartCount} chars | End: -{TrimEndCount} chars";

    public override string Apply(string currentBaseName, RenameContext context)
    {
        string result = currentBaseName;

        if (TrimStartCount > 0)
            result = result.Length > TrimStartCount ? result[TrimStartCount..] : string.Empty;

        if (TrimEndCount > 0)
            result = result.Length > TrimEndCount ? result[..^TrimEndCount] : string.Empty;

        return result.Trim();
    }
}

public sealed class CaseTransformOperation : RenameOperation
{
    public CaseTransform CaseMode { get; set; } = CaseTransform.TitleCase;

    public override string DisplayTitle => "Case Conversion";
    public override string Summary => CaseMode.ToString();

    public override string Apply(string currentBaseName, RenameContext context)
    {
        if (CaseMode == CaseTransform.None)
            return currentBaseName;

        string[] segments = currentBaseName.Split(" - ", StringSplitOptions.TrimEntries);
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = ConvertCase(segments[i], CaseMode);
        }
        return string.Join(" - ", segments);
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