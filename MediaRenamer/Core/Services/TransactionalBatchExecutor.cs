using MediaRenamer.Core.Abstractions;
using MediaRenamer.Core.Infrastructure;
using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class TransactionalBatchExecutor(IFileSystem? fileSystem = null)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new PhysicalFileSystem();
    private readonly Stack<IReadOnlyList<RenameRecord>> _undoHistory = new();

    public bool CanUndo => _undoHistory.Count > 0;
    public int LastBatchCount => _undoHistory.TryPeek(out var last) ? last.Count : 0;

    public BatchExecutionResult Execute(IReadOnlyList<RenameOperationPreview> previews)
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

    public BatchExecutionResult UndoLast()
    {
        if (!_undoHistory.TryPop(out var lastBatch))
            return new BatchExecutionResult(0, ["Nothing to undo."], []);

        var reversedPairs = lastBatch
            .Select(r => new RenameRecord(r.NewFullPath, r.OriginalFullPath))
            .ToList();

        return ExecuteMovePairs(reversedPairs);
    }

    private BatchExecutionResult ExecuteMovePairs(IReadOnlyList<RenameRecord> moves)
    {
        int succeeded = 0;
        var errors = new List<string>();
        var applied = new List<RenameRecord>();
        var tempMoves = new List<(string TempPath, RenameRecord Record)>();

        foreach (var record in moves)
        {
            try
            {
                if (!_fileSystem.FileExists(record.OriginalFullPath))
                {
                    errors.Add($"Missing source file: {Path.GetFileName(record.OriginalFullPath)}");
                    continue;
                }

                string dir = Path.GetDirectoryName(record.OriginalFullPath)!;
                string ext = Path.GetExtension(record.OriginalFullPath);
                string tempPath = Path.Combine(dir, $".tmp_rename_{Guid.NewGuid():N}{ext}");
                _fileSystem.MoveFile(record.OriginalFullPath, tempPath);
                tempMoves.Add((tempPath, record));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(record.OriginalFullPath)}: {ex.Message}");
            }
        }

        foreach (var (tempPath, record) in tempMoves)
        {
            try
            {
                _fileSystem.MoveFile(tempPath, record.NewFullPath);
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