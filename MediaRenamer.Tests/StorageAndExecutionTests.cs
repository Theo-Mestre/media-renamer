using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Core.Services;
using Xunit;

namespace MediaRenamer.Tests;

public sealed class StorageAndExecutionTests
{
    [Fact]
    public void TransactionalExecution_And_Undo_RestoresOriginalFilesInMemory()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(@"X:\Media");
        fs.WriteAllText(@"X:\Media\Knight - StudioA - 002.png", "img2");
        fs.WriteAllText(@"X:\Media\Knight - StudioA - 005.png", "img5");

        var scanner = new WorkspaceScanner(fs);
        var engine = new RenameEngine(
            collisionDetector: new CollisionDetector(fs),
            batchExecutor: new TransactionalBatchExecutor(fs));

        var files = scanner.ScanDirectory(@"X:\Media");
        var config = new RenamePipelineConfig { Operations = [], CompactGaps = true };

        var previews = engine.BuildPreview(files, files, config);
        var execResult = engine.ExecuteRenames(previews);

        Assert.Equal(2, execResult.Succeeded);
        Assert.True(fs.FileExists(@"X:\Media\Knight - StudioA - 001.png"));
        Assert.True(fs.FileExists(@"X:\Media\Knight - StudioA - 002.png"));
        Assert.False(fs.FileExists(@"X:\Media\Knight - StudioA - 005.png"));

        var undoResult = engine.UndoLastBatch();
        Assert.Equal(2, undoResult.Succeeded);
        Assert.True(fs.FileExists(@"X:\Media\Knight - StudioA - 002.png"));
        Assert.True(fs.FileExists(@"X:\Media\Knight - StudioA - 005.png"));
        Assert.False(fs.FileExists(@"X:\Media\Knight - StudioA - 001.png"));
    }

    [Fact]
    public void JsonPresetRepository_SavesAndReloadsPolymorphicOperations()
    {
        var fs = new InMemoryFileSystem();
        var repo = new JsonPresetRepository(fs, @"X:\Presets");

        var config = new RenamePipelineConfig
        {
            Operations =
            [
                new ReplaceStringOperation { SearchFor = "_", ReplaceWith = " " },
                new AddAffixOperation { Prefix = "! " }
            ],
            AutoCount = true
        };

        repo.Save("my-custom", config);
        var loaded = repo.LoadAll().Single(p => p.Name == "my custom");

        Assert.Equal(2, loaded.Config.Operations.Count);
        Assert.IsType<ReplaceStringOperation>(loaded.Config.Operations[0]);
        Assert.IsType<AddAffixOperation>(loaded.Config.Operations[1]);
        Assert.True(loaded.Config.AutoCount);
    }
}