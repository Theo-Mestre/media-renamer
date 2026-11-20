using MediaRenamer.Core.Pipeline;

namespace MediaRenamer.Core.Models;

public sealed class RenamePipelineConfig
{
    public List<RenameOperation> Operations { get; set; } =
    [
        new StripLegacyNumberOperation()
    ];

    public bool AutoCount { get; set; } = true;
    public bool CompactGaps { get; set; } = false;
    public CountScope Scope { get; set; } = CountScope.CharacterOnly;
    public int PaddingDigits { get; set; } = 3;
    public int? LinkedNumberOverride { get; set; }

    public void CopyFrom(RenamePipelineConfig other)
    {
        Operations = [.. other.Operations];
        AutoCount = other.AutoCount;
        CompactGaps = other.CompactGaps;
        Scope = other.Scope;
        PaddingDigits = other.PaddingDigits;
        LinkedNumberOverride = other.LinkedNumberOverride;
    }

    public void Reset()
    {
        Operations = [new StripLegacyNumberOperation()];
        AutoCount = false;
        CompactGaps = false;
        Scope = CountScope.NameAndArtist;
        PaddingDigits = 3;
        LinkedNumberOverride = null;
    }
}