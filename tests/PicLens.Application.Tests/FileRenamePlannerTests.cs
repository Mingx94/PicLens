using PicLens.Application.Services;

namespace PicLens.Application.Tests;

public sealed class FileRenamePlannerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nested\\photo.jpg")]
    [InlineData("nested/photo.jpg")]
    [InlineData("bad:name.jpg")]
    [InlineData("photo.txt")]
    public void ValidateImageFileName_rejects_unsafe_or_unsupported_names(string fileName)
    {
        var result = FileRenamePlanner.ValidateImageFileName(fileName);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void ValidateImageFileName_accepts_supported_leaf_file_name()
    {
        var result = FileRenamePlanner.ValidateImageFileName("Renamed Photo.webp");

        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void PlanDropTargetBatchRename_preserves_extensions_excludes_target_and_skips_existing_sequence_names()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Album.jpg");
        var sources = new[]
        {
            Path.Combine(root, "first.png"),
            target,
            Path.Combine(root, "Album-02.gif"),
            Path.Combine(root, "third.webp")
        };

        var plan = FileRenamePlanner.PlanDropTargetBatchRename(
            sources,
            target,
            targetExists: path => Path.GetFileName(path).Equals("Album-03.webp", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(3, plan.Total);
        Assert.Collection(
            plan.Items,
            item =>
            {
                Assert.Equal(sources[0], item.SourcePath);
                Assert.Equal(Path.Combine(root, "Album-01.png"), item.TargetPath);
                Assert.False(item.ShouldSkip);
            },
            item =>
            {
                Assert.Equal(sources[2], item.SourcePath);
                Assert.Equal(Path.Combine(root, "Album-02.gif"), item.TargetPath);
                Assert.True(item.ShouldSkip);
                Assert.Equal("already_target_sequence", item.Reason);
            },
            item =>
            {
                Assert.Equal(sources[3], item.SourcePath);
                Assert.Equal(Path.Combine(root, "Album-02.webp"), item.TargetPath);
                Assert.False(item.ShouldSkip);
            });
    }

    [Fact]
    public void PlanDropTargetBatchRename_advances_past_existing_target_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Album.jpg");
        var sources = new[]
        {
            Path.Combine(root, "first.png"),
            Path.Combine(root, "second.webp")
        };
        var existingTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(root, "Album-01.png"),
            Path.Combine(root, "Album-03.webp")
        };

        var plan = FileRenamePlanner.PlanDropTargetBatchRename(
            sources,
            target,
            targetExists: existingTargets.Contains);

        Assert.Collection(
            plan.Items,
            item =>
            {
                Assert.Equal(sources[0], item.SourcePath);
                Assert.Equal(Path.Combine(root, "Album-02.png"), item.TargetPath);
                Assert.False(item.ShouldSkip);
            },
            item =>
            {
                Assert.Equal(sources[1], item.SourcePath);
                Assert.Equal(Path.Combine(root, "Album-04.webp"), item.TargetPath);
                Assert.False(item.ShouldSkip);
            });
    }

    [Fact]
    public void PlanDropTargetBatchRename_skips_any_existing_target_sequence_source_name()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Album.jpg");
        var source = Path.Combine(root, "Album-99.png");

        var plan = FileRenamePlanner.PlanDropTargetBatchRename([source], target, targetExists: _ => false);

        var item = Assert.Single(plan.Items);
        Assert.True(item.ShouldSkip);
        Assert.Equal("already_target_sequence", item.Reason);
    }
}
