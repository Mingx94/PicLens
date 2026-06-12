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
    public void PlanDropTargetBatchRename_preserves_extensions_excludes_target_and_reserves_sequence_names_without_extensions()
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
            targetNameExists: TargetNameExists(
            [
                sources[0],
                sources[2],
                sources[3],
                Path.Combine(root, "Album-03.webp")
            ]));

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
                Assert.Equal(Path.Combine(root, "Album-04.webp"), item.TargetPath);
                Assert.False(item.ShouldSkip);
            });
    }

    [Fact]
    public void PlanDropTargetBatchRename_advances_past_existing_sequence_names_without_extensions()
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
            Path.Combine(root, "Album-01.jpg"),
            Path.Combine(root, "Album-03.webp")
        };

        var plan = FileRenamePlanner.PlanDropTargetBatchRename(
            sources,
            target,
            targetNameExists: TargetNameExists(existingTargets));

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
    public void PlanDropTargetBatchRename_compacts_existing_sequence_source_into_first_gap()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Album.jpg");
        var source = Path.Combine(root, "Album-03.jpg");

        var plan = FileRenamePlanner.PlanDropTargetBatchRename([source], target, TargetNameExists([source]));

        var item = Assert.Single(plan.Items);
        Assert.Equal(source, item.SourcePath);
        Assert.Equal(Path.Combine(root, "Album-01.jpg"), item.TargetPath);
        Assert.False(item.ShouldSkip);
    }

    [Fact]
    public void PlanDropTargetBatchRename_skips_existing_sequence_source_when_no_earlier_gap_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "Album.jpg");
        var source = Path.Combine(root, "Album-03.jpg");

        var plan = FileRenamePlanner.PlanDropTargetBatchRename(
            [source],
            target,
            TargetNameExists(
            [
                Path.Combine(root, "Album-01.png"),
                Path.Combine(root, "Album-02.webp"),
                source
            ]));

        var item = Assert.Single(plan.Items);
        Assert.True(item.ShouldSkip);
        Assert.Equal("already_target_sequence", item.Reason);
    }

    private static Func<string, string, bool> TargetNameExists(IEnumerable<string> existingPaths)
    {
        var paths = existingPaths.ToList();

        return (candidatePath, sourcePath) => paths.Any(path =>
            !PathEquals(path, sourcePath)
            && PathEquals(Path.GetDirectoryName(path), Path.GetDirectoryName(candidatePath))
            && string.Equals(
                Path.GetFileNameWithoutExtension(path),
                Path.GetFileNameWithoutExtension(candidatePath),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool PathEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
