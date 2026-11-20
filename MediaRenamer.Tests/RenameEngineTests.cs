using MediaRenamer.Core.Models;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Core.Services;
using Xunit;

namespace MediaRenamer.Tests;

public sealed class RenameEngineTests
{
    private static RenameEngine CreateInMemoryEngine(params string[] existingPaths)
    {
        var fs = new InMemoryFileSystem(existingPaths);
        return new RenameEngine(collisionDetector: new CollisionDetector(fs));
    }

    [Fact]
    public void RegexSwap_And_AutoCount_ContinuesAcrossFolders()
    {
        var root = @"X:\Media";
        var existing = new MediaFileItem($@"{root}\Archive\Knight - StudioA - 001.png", root);
        var inbox = new MediaFileItem($@"{root}\Inbox\StudioA Knight.mp4", root);

        var engine = CreateInMemoryEngine(existing.FullPath, inbox.FullPath);
        var config = new RenamePipelineConfig
        {
            Operations =
            [
                new ReplaceStringOperation
                {
                    SearchFor = @"^(?<artist>\S+)\s+(?<name>.+)$",
                    ReplaceWith = "${name} - ${artist}",
                    UseRegex = true
                }
            ],
            AutoCount = true
        };

        var previews = engine.BuildPreview([existing, inbox], [inbox], config);

        Assert.Single(previews);
        Assert.Equal("Knight - StudioA - 002.mp4", previews[0].ProposedFileName);
        Assert.False(previews[0].HasCollision);
    }

    [Fact]
    public void CompactGaps_RenumbersWithoutSelfCollision()
    {
        var root = @"X:\Media";
        var f1 = new MediaFileItem($@"{root}\Knight - StudioA - 001.png", root);
        var f4 = new MediaFileItem($@"{root}\Knight - StudioA - 004.jpg", root);
        var f7 = new MediaFileItem($@"{root}\Knight - StudioA - 007.mp4", root);

        var engine = CreateInMemoryEngine(f1.FullPath, f4.FullPath, f7.FullPath);
        var config = new RenamePipelineConfig
        {
            Operations = [],
            CompactGaps = true
        };

        var previews = engine.BuildPreview([f1, f4, f7], [f4], config);

        Assert.Equal("Knight - StudioA - 001.png", previews[0].ProposedFileName);
        Assert.Equal("Knight - StudioA - 002.jpg", previews[1].ProposedFileName);
        Assert.Equal("Knight - StudioA - 003.mp4", previews[2].ProposedFileName);
        Assert.All(previews, p => Assert.False(p.HasCollision));
    }

    [Fact]
    public void NameOnlyScope_CountsAcrossDifferentArtistsForSameLicenseAndCharacter()
    {
        var root = @"X:\Media";
        var f1 = new MediaFileItem($@"{root}\Pokemon - Reshiram - Burgerkiss - 001.mp4", root);
        var f2 = new MediaFileItem($@"{root}\Pokemon - Reshiram - Doctorslm - 001.mp4", root);
        var f3 = new MediaFileItem($@"{root}\Pokemon - Reshiram - Niki3d - 001.mp4", root);

        var engine = CreateInMemoryEngine(f1.FullPath, f2.FullPath, f3.FullPath);
        var config = new RenamePipelineConfig
        {
            Operations = [new StripLegacyNumberOperation()],
            AutoCount = true,
            Scope = CountScope.NameOnly
        };

        var previews = engine.BuildPreview([f1, f2, f3], [f1, f2, f3], config);

        Assert.Equal("Pokemon - Reshiram - Burgerkiss - 001.mp4", previews[0].ProposedFileName);
        Assert.Equal("Pokemon - Reshiram - Doctorslm - 002.mp4", previews[1].ProposedFileName);
        Assert.Equal("Pokemon - Reshiram - Niki3d - 003.mp4", previews[2].ProposedFileName);
    }

    [Fact]
    public void CharacterOnlyScope_CountsAcrossTwoAndThreeSegmentFilesOnDisk()
    {
        var root = @"X:\Media";
        var twoSegmentExisting = new MediaFileItem($@"{root}\Archive\Reshiram - StudioA - 001.png", root);
        var threeSegmentNew = new MediaFileItem($@"{root}\Inbox\Pokemon - Reshiram - StudioB - 001.mp4", root);

        var engine = CreateInMemoryEngine(twoSegmentExisting.FullPath, threeSegmentNew.FullPath);
        var config = new RenamePipelineConfig
        {
            Operations = [new StripLegacyNumberOperation()],
            AutoCount = true,
            Scope = CountScope.CharacterOnly
        };

        var previews = engine.BuildPreview([twoSegmentExisting, threeSegmentNew], [threeSegmentNew], config);

        Assert.Single(previews);
        Assert.Equal("Pokemon - Reshiram - StudioB - 002.mp4", previews[0].ProposedFileName);
    }
}