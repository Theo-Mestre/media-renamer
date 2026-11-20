using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Core.Services;
using MediaRenamer.UI.Operations;
using Xunit;

namespace MediaRenamer.Tests;

public sealed class PipelineAndDescriptorTests
{
    [Fact]
    public void EveryConcreteRenameOperation_HasRegisteredUiDescriptor()
    {
        var operationTypes = typeof(RenameOperation).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(RenameOperation).IsAssignableFrom(t))
            .ToList();

        var registeredTargets = OperationEditorRegistry.All
            .Select(d => d.TargetOperationType)
            .ToHashSet();

        foreach (var opType in operationTypes)
        {
            Assert.Contains(opType, registeredTargets);
        }
    }

    [Fact]
    public void MultiStepPipeline_RecordsStepSnapshots_And_ReplacesWithWhitespace()
    {
        var root = @"X:\Media";
        var file = new MediaFileItem($@"{root}\Silver_Knight_StudioA_01.png", root);

        var fs = new InMemoryFileSystem(file.FullPath);
        var engine = new RenameEngine(collisionDetector: new CollisionDetector(fs));

        var config = new RenamePipelineConfig
        {
            Operations =
            [
                new StripLegacyNumberOperation(),
                new ReplaceStringOperation { SearchFor = "_", ReplaceWith = " " },
                new SplitNameArtistOperation { ArtistHint = "StudioA" }
            ],
            AutoCount = true
        };

        var previews = engine.BuildPreview([file], [file], config);

        Assert.Single(previews);
        var p = previews[0];
        Assert.Equal(3, p.StepHistory.Count);
        Assert.Equal("Silver_Knight_StudioA", p.StepHistory[0].ResultingBaseName);
        Assert.Equal("Silver Knight StudioA", p.StepHistory[1].ResultingBaseName);
        Assert.Equal("Silver Knight - StudioA", p.StepHistory[2].ResultingBaseName);
        Assert.Equal("Silver Knight - StudioA - 001.png", p.ProposedFileName);
    }
}