using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;

namespace MediaRenamer.Core.Services;

public sealed record PipelineTransformOutput(
    string FinalStem,
    IReadOnlyList<StepPreviewSnapshot> Snapshots
);

public sealed class OperationPipelineRunner
{
    public PipelineTransformOutput Run(
        string initialBaseName,
        IReadOnlyList<RenameOperation> operations,
        RenamePipelineConfig config)
    {
        var ctx = new RenameContext { PreservedPadding = config.PaddingDigits };
        var enabledOps = operations.Where(op => op.IsEnabled).ToList();
        var snapshots = new List<StepPreviewSnapshot>(enabledOps.Count);
        string current = initialBaseName;

        foreach (var op in enabledOps)
        {
            current = op.Apply(current, ctx);
            snapshots.Add(new StepPreviewSnapshot(op.Id, op.DisplayTitle, current));
        }

        // Re-attach preserved index when no automated numbering mode overrides it
        if (!config.AutoCount && !config.CompactGaps && !config.LinkedNumberOverride.HasValue && ctx.PreservedIndex.HasValue)
        {
            current = $"{current} - {ctx.PreservedIndex.Value.ToString($"D{ctx.PreservedPadding}")}";
        }

        return new PipelineTransformOutput(current, snapshots);
    }
}