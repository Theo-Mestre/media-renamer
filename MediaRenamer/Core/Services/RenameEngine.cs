using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Services;

public sealed class RenameEngine(
    OperationPipelineRunner? pipelineRunner = null,
    SequenceNumberingService? numberingService = null,
    CollisionDetector? collisionDetector = null,
    TransactionalBatchExecutor? batchExecutor = null)
{
    private readonly OperationPipelineRunner _pipelineRunner = pipelineRunner ?? new OperationPipelineRunner();
    private readonly SequenceNumberingService _numberingService = numberingService ?? new SequenceNumberingService();
    private readonly CollisionDetector _collisionDetector = collisionDetector ?? new CollisionDetector();
    private readonly TransactionalBatchExecutor _batchExecutor = batchExecutor ?? new TransactionalBatchExecutor();

    public bool CanUndo => _batchExecutor.CanUndo;
    public int LastBatchCount => _batchExecutor.LastBatchCount;

    public List<RenameOperationPreview> BuildPreview(
        IReadOnlyList<MediaFileItem> allWorkspaceFiles,
        IReadOnlyList<MediaFileItem> selectedFiles,
        RenamePipelineConfig config)
    {
        var selectedPaths = selectedFiles
            .Select(f => f.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var transformedMap = new Dictionary<string, PipelineTransformOutput>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in selectedFiles)
        {
            transformedMap[file.FullPath] = _pipelineRunner.Run(file.BaseName, config.Operations, config);
        }

        var numbered = _numberingService.ApplyNumbering(allWorkspaceFiles, selectedPaths, transformedMap, config);
        return _collisionDetector.BuildValidatedPreviews(numbered);
    }

    public BatchExecutionResult ExecuteRenames(IReadOnlyList<RenameOperationPreview> previews)
        => _batchExecutor.Execute(previews);

    public BatchExecutionResult UndoLastBatch()
        => _batchExecutor.UndoLast();
}