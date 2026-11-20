using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class CollisionDetector(IFileSystem? fileSystem = null)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new PhysicalFileSystem();

    public List<RenameOperationPreview> BuildValidatedPreviews(
        IReadOnlyList<(NumberingCandidate Candidate, string FinalStem)> numberedItems)
    {
        var results = new List<RenameOperationPreview>(numberedItems.Count);
        var plannedTargetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var vacatedSourcePaths = numberedItems
            .Where(x => !string.Equals(x.Candidate.File.BaseName, x.FinalStem, StringComparison.Ordinal))
            .Select(x => x.Candidate.File.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (candidate, finalStem) in numberedItems)
        {
            string finalFileName = finalStem + candidate.File.Extension;
            string dir = Path.GetDirectoryName(candidate.File.FullPath)!;
            string finalFullPath = Path.Combine(dir, finalFileName);

            bool hasChanged = !string.Equals(candidate.File.FileName, finalFileName, StringComparison.Ordinal);
            bool hasInvalidChars = finalFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

            bool diskConflict = hasChanged &&
                                _fileSystem.FileExists(finalFullPath) &&
                                !vacatedSourcePaths.Contains(finalFullPath);

            bool batchConflict = hasChanged && plannedTargetPaths.Contains(finalFullPath);
            bool hasCollision = hasInvalidChars || diskConflict || batchConflict;

            plannedTargetPaths.Add(finalFullPath);

            results.Add(new RenameOperationPreview(
                candidate.File,
                candidate.StepHistory,
                finalFileName,
                finalFullPath,
                hasChanged,
                hasCollision));
        }

        return results;
    }
}