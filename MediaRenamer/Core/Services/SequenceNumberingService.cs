using MediaRenamer.Core.Models;
using MediaRenamer.Core.Parsing;

namespace MediaRenamer.Core.Services;

public sealed record NumberingCandidate(
    MediaFileItem File,
    bool IsDirectlySelected,
    string TransformedStem,
    IReadOnlyList<StepPreviewSnapshot> StepHistory
);

public sealed class SequenceNumberingService
{
    public IReadOnlyList<(NumberingCandidate Candidate, string FinalStem)> ApplyNumbering(
        IReadOnlyList<MediaFileItem> allWorkspaceFiles,
        IReadOnlySet<string> selectedPaths,
        IReadOnlyDictionary<string, PipelineTransformOutput> transformedMap,
        RenamePipelineConfig config)
    {
        var targetScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in selectedPaths)
        {
            if (transformedMap.TryGetValue(path, out var output))
            {
                var parsed = MediaNameParser.Parse(output.FinalStem + ".tmp");
                targetScopes.Add(parsed.GetScopeKey(config.Scope));
            }
        }

        bool pullEntireScope = config.LinkedNumberOverride.HasValue || config.CompactGaps;

        var counter = config.CompactGaps
            ? new CrossFolderCounter([], selectedPaths, config.Scope)
            : new CrossFolderCounter(allWorkspaceFiles, selectedPaths, config.Scope);

        var candidates = allWorkspaceFiles
            .Where(f => selectedPaths.Contains(f.FullPath) ||
                        (pullEntireScope && targetScopes.Contains(f.Parsed.GetScopeKey(config.Scope))))
            .OrderBy(f => f.Parsed.IndexNumber ?? int.MaxValue)
            .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(f =>
            {
                bool isSelected = selectedPaths.Contains(f.FullPath);
                var transform = isSelected ? transformedMap[f.FullPath] : null;
                return new NumberingCandidate(
                    f,
                    isSelected,
                    transform?.FinalStem ?? f.BaseName,
                    transform?.Snapshots ?? []);
            })
            .ToList();

        var results = new List<(NumberingCandidate Candidate, string FinalStem)>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var parsed = MediaNameParser.Parse(candidate.TransformedStem + candidate.File.Extension);
            string scopeKey = parsed.GetScopeKey(config.Scope);
            string finalStem = candidate.TransformedStem;

            if (config.LinkedNumberOverride.HasValue && targetScopes.Contains(scopeKey))
            {
                string formatted = config.LinkedNumberOverride.Value.ToString($"D{config.PaddingDigits}");
                finalStem = $"{parsed.IdentityPrefix} - {formatted}";
            }
            else if (config.CompactGaps && targetScopes.Contains(scopeKey))
            {
                int nextNum = counter.NextIndexFor(scopeKey);
                string formatted = nextNum.ToString($"D{config.PaddingDigits}");
                finalStem = $"{parsed.IdentityPrefix} - {formatted}";
            }
            else if (candidate.IsDirectlySelected && config.AutoCount)
            {
                int nextNum = counter.NextIndexFor(scopeKey);
                string formatted = nextNum.ToString($"D{config.PaddingDigits}");
                finalStem = $"{parsed.IdentityPrefix} - {formatted}";
            }

            results.Add((candidate, finalStem));
        }

        return results;
    }
}