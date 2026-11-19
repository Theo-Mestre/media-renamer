using MediaRenamer.Core.Models;

namespace MediaRenamer.Core.Pipeline;

public sealed class RenameContext
{
    public required RenamePipelineConfig Config { get; init; }
    public int? PreservedIndex { get; set; }
    public int PreservedPadding { get; set; } = 3;
}

public interface IRenameStep
{
    string Execute(string currentBaseName, RenameContext context);
}