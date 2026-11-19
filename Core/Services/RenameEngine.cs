using MediaRenamer.Core.Models;
using MediaRenamer.Core.Parsing;
using MediaRenamer.Core.Pipeline;

namespace MediaRenamer.Core.Services;

public sealed class RenameEngine
{
    private readonly IReadOnlyList<IRenameStep> _pipelineSteps;
    private readonly Stack<IReadOnlyList<RenameRecord>> _undoHistory = new();

    public bool CanUndo => _undoHistory.Count > 0;
    public int LastBatchCount => _undoHistory.TryPeek(out var last) ? last.Count : 0;

    public RenameEngine()
    {
        _pipelineSteps =
        [
            new StripLegacyNumberStep(),
            new SplitNameArtistStep(),
            new SetBaseIdentityStep(),
            new FindReplaceStep(),
            new AffixStep(),
            new CaseTransformStep()
        ];
    }

    public List<RenameOperationPreview> BuildPreview(
        IReadOnlyList<MediaFileItem> allWorkspaceFiles,
        IReadOnlyList<MediaFileItem> selectedFiles,
        RenamePipelineConfig config)
    {
        var selectedPaths = selectedFiles.Select(f => f.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Run text transformation pipeline on selected files
        var transformedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in selectedFiles)
        {
            var ctx = new RenameContext { Config = config, PreservedPadding = config.PaddingDigits };
            string current = file.BaseName;

            foreach (var step in _pipelineSteps)
            {
                current = step.Execute(current, ctx);
            }

            transformedMap[file.FullPath] = current;
        }

        // 2. Collect target scopes from selected files (used by LinkedNumberOverride and CompactGaps)
        var targetScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in selectedFiles)
        {
            var parsed = MediaNameParser.Parse(transformedMap[file.FullPath] + file.Extension);
            targetScopes.Add(parsed.GetScopeKey(config.Scope));
        }

        bool pullEntireScope = config.LinkedNumberOverride.HasValue || config.CompactGaps;

        // 3. Initialize cross-folder counter:
        //    - If CompactGaps is ON, start every affected scope at 0 so numbering compacts from 001
        //    - Otherwise, seed max existing index from unselected workspace files
        var counter = config.CompactGaps
            ? new CrossFolderCounter([], selectedPaths, config.Scope)
            : new CrossFolderCounter(allWorkspaceFiles, selectedPaths, config.Scope);

        // 4. Gather files to evaluate and sort cleanly by existing index when compacting gaps
        var filesToEvaluate = allWorkspaceFiles
            .Where(f => selectedPaths.Contains(f.FullPath) ||
                        (pullEntireScope && targetScopes.Contains(f.Parsed.GetScopeKey(config.Scope))))
            .OrderBy(f => f.Parsed.IndexNumber ?? int.MaxValue)
            .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 5. Assemble final previews
        var results = new List<RenameOperationPreview>();
        var plannedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in filesToEvaluate)
        {
            bool isDirectlySelected = selectedPaths.Contains(file.FullPath);
            string workingBase = isDirectlySelected ? transformedMap[file.FullPath] : file.BaseName;
            var parsed = MediaNameParser.Parse(workingBase + file.Extension);
            string scopeKey = parsed.GetScopeKey(config.Scope);

            string finalBaseName = workingBase;

            if (config.LinkedNumberOverride.HasValue && targetScopes.Contains(scopeKey))
            {
                string formattedNum = config.LinkedNumberOverride.Value.ToString($"D{config.PaddingDigits}");
                finalBaseName = $"{parsed.IdentityPrefix} - {formattedNum}";
            }
            else if (config.CompactGaps && targetScopes.Contains(scopeKey))
            {
                int nextNum = counter.NextIndexFor(scopeKey);
                string formattedNum = nextNum.ToString($"D{config.PaddingDigits}");
                finalBaseName = $"{parsed.IdentityPrefix} - {formattedNum}";
            }
            else if (isDirectlySelected && config.AutoCount)
            {
                int nextNum = counter.NextIndexFor(scopeKey);
                string formattedNum = nextNum.ToString($"D{config.PaddingDigits}");
                finalBaseName = $"{parsed.IdentityPrefix} - {formattedNum}";
            }

            string finalFileName = finalBaseName + file.Extension;
            string dir = Path.GetDirectoryName(file.FullPath)!;
            string finalFullPath = Path.Combine(dir, finalFileName);

            bool hasChanged = !string.Equals(file.FileName, finalFileName, StringComparison.Ordinal);
            bool hasCollision = hasChanged &&
                                (File.Exists(finalFullPath) || plannedTargetPaths.Contains(finalFullPath));

            plannedTargetPaths.Add(finalFullPath);
            results.Add(new RenameOperationPreview(file, finalFileName, finalFullPath, hasChanged, hasCollision));
        }

        return results;
    }

    public BatchExecutionResult ExecuteRenames(IReadOnlyList<RenameOperationPreview> previews)
    {
        var actionable = previews
            .Where(p => p.HasChanged && !p.HasCollision)
            .Select(p => new RenameRecord(p.Source.FullPath, p.ProposedFullPath))
            .ToList();

        var result = ExecuteMovePairs(actionable);
        if (result.AppliedRecords.Count > 0)
        {
            _undoHistory.Push(result.AppliedRecords);
        }
        return result;
    }

    public BatchExecutionResult UndoLastBatch()
    {
        if (!_undoHistory.TryPop(out var lastBatch))
            return new BatchExecutionResult(0, ["Nothing to undo."], []);

        var reversedPairs = lastBatch
            .Select(r => new RenameRecord(r.NewFullPath, r.OriginalFullPath))
            .ToList();

        return ExecuteMovePairs(reversedPairs);
    }

    private static BatchExecutionResult ExecuteMovePairs(IReadOnlyList<RenameRecord> moves)
    {
        int succeeded = 0;
        var errors = new List<string>();
        var applied = new List<RenameRecord>();
        var tempMoves = new List<(string TempPath, RenameRecord Record)>();

        // Phase 1: Stage to temporary filenames to prevent case-only or swap collisions
        foreach (var record in moves)
        {
            try
            {
                if (!File.Exists(record.OriginalFullPath))
                {
                    errors.Add($"Missing source file: {Path.GetFileName(record.OriginalFullPath)}");
                    continue;
                }

                string dir = Path.GetDirectoryName(record.OriginalFullPath)!;
                string ext = Path.GetExtension(record.OriginalFullPath);
                string tempPath = Path.Combine(dir, $".tmp_rename_{Guid.NewGuid():N}{ext}");
                File.Move(record.OriginalFullPath, tempPath);
                tempMoves.Add((tempPath, record));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(record.OriginalFullPath)}: {ex.Message}");
            }
        }

        // Phase 2: Commit final filenames
        foreach (var (tempPath, record) in tempMoves)
        {
            try
            {
                File.Move(tempPath, record.NewFullPath);
                succeeded++;
                applied.Add(record);
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(record.OriginalFullPath)} -> {Path.GetFileName(record.NewFullPath)}: {ex.Message}");
            }
        }

        return new BatchExecutionResult(succeeded, errors, applied);
    }
}