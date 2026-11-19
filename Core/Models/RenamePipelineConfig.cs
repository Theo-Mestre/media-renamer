namespace MediaRenamer.Core.Models;

public sealed class RenamePipelineConfig
{
    public string? SetBaseIdentity { get; set; }
    public string? FindText { get; set; }
    public string ReplaceText { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public CaseTransform CaseMode { get; set; } = CaseTransform.None;

    public int? SplitNameAfterWords { get; set; }
    public string? ArtistDelimiterHint { get; set; }

    public bool StripLegacyNumbers { get; set; } = true;
    public bool AutoCount { get; set; } = false;
    public bool CompactGaps { get; set; } = false;
    public CountScope Scope { get; set; } = CountScope.NameAndArtist;
    public int PaddingDigits { get; set; } = 3;

    public int? LinkedNumberOverride { get; set; }

    public void CopyFrom(RenamePipelineConfig other)
    {
        SetBaseIdentity = other.SetBaseIdentity;
        FindText = other.FindText;
        ReplaceText = other.ReplaceText;
        Prefix = other.Prefix;
        Suffix = other.Suffix;
        CaseMode = other.CaseMode;
        SplitNameAfterWords = other.SplitNameAfterWords;
        ArtistDelimiterHint = other.ArtistDelimiterHint;
        StripLegacyNumbers = other.StripLegacyNumbers;
        AutoCount = other.AutoCount;
        CompactGaps = other.CompactGaps;
        Scope = other.Scope;
        PaddingDigits = other.PaddingDigits;
        LinkedNumberOverride = other.LinkedNumberOverride;
    }

    public void Reset()
    {
        SetBaseIdentity = null;
        FindText = null;
        ReplaceText = string.Empty;
        Prefix = null;
        Suffix = null;
        CaseMode = CaseTransform.None;
        SplitNameAfterWords = null;
        ArtistDelimiterHint = null;
        StripLegacyNumbers = true;
        AutoCount = false;
        CompactGaps = false;
        Scope = CountScope.NameAndArtist;
        LinkedNumberOverride = null;
    }
}