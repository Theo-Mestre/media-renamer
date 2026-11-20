using MediaRenamer.Core.Parsing;

namespace MediaRenamer.Core.Models;

public enum CountScope
{
    NameAndArtist,
    NameOnly
}

public enum CaseTransform
{
    None,
    TitleCase,
    PascalCase,
    CamelCase
}

public sealed record ParsedMediaName(
    string RawBaseName,
    string Extension,
    string NamePart,
    string ArtistPart,
    int? IndexNumber,
    int PaddingWidth
)
{
    public string IdentityPrefix => string.IsNullOrWhiteSpace(ArtistPart)
        ? NamePart
        : $"{NamePart} - {ArtistPart}";

    public bool IsConforming =>
        !string.IsNullOrWhiteSpace(NamePart) &&
        !string.IsNullOrWhiteSpace(ArtistPart) &&
        IndexNumber.HasValue &&
        PaddingWidth >= 3 &&
        !MediaNameParser.LooseTrailingNumberRegex().IsMatch(ArtistPart);

    public string NonConformingReason
    {
        get
        {
            if (IsConforming) return "OK";
            if (!IndexNumber.HasValue) return "Missing ' - 001' index";
            if (PaddingWidth < 3) return $"Index < 3 digits ({PaddingWidth})";
            if (string.IsNullOrWhiteSpace(ArtistPart)) return "Missing ' - Artist'";
            if (MediaNameParser.LooseTrailingNumberRegex().IsMatch(ArtistPart)) return "Unsplit trailing number";
            return "Invalid format";
        }
    }

    public string GetScopeKey(CountScope scope) => scope switch
    {
        CountScope.NameAndArtist => string.IsNullOrWhiteSpace(ArtistPart)
            ? NamePart.Trim().ToLowerInvariant()
            : $"{NamePart.Trim().ToLowerInvariant()}|{ArtistPart.Trim().ToLowerInvariant()}",

        CountScope.NameOnly => IdentityPrefix.Contains(" - ")
            ? NamePart.Trim().ToLowerInvariant()
            : IdentityPrefix.Trim().ToLowerInvariant(),

        _ => NamePart.Trim().ToLowerInvariant()
    };
}

public sealed class MediaFileItem
{
    public string FullPath { get; }
    public string RootDirectory { get; }
    public string RelativeFolder { get; }
    public string FileName { get; }
    public string BaseName { get; }
    public string Extension { get; }
    public ParsedMediaName Parsed { get; }

    public MediaFileItem(string fullPath, string rootDirectory)
    {
        FullPath = fullPath;
        RootDirectory = rootDirectory;
        RelativeFolder = Path.GetRelativePath(rootDirectory, Path.GetDirectoryName(fullPath)!);
        FileName = Path.GetFileName(fullPath);
        BaseName = Path.GetFileNameWithoutExtension(fullPath);
        Extension = Path.GetExtension(fullPath);
        Parsed = MediaNameParser.Parse(FileName);
    }
}

public sealed record StepPreviewSnapshot(
    string OperationId,
    string OperationTitle,
    string ResultingBaseName
);

public sealed record RenameOperationPreview(
    MediaFileItem Source,
    IReadOnlyList<StepPreviewSnapshot> StepHistory,
    string ProposedFileName,
    string ProposedFullPath,
    bool HasChanged,
    bool HasCollision
);

public sealed record RenameRecord(string OriginalFullPath, string NewFullPath);

public sealed record BatchExecutionResult(
    int Succeeded,
    IReadOnlyList<string> Errors,
    IReadOnlyList<RenameRecord> AppliedRecords
);