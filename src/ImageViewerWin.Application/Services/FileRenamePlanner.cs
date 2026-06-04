using ImageViewerWin.Core.Domain;

namespace ImageViewerWin.Application.Services;

public static class FileRenamePlanner
{
    private const string AlreadyTargetSequenceReason = "already_target_sequence";

    public static FileNameValidationResult ValidateImageFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new FileNameValidationResult(false, "empty_name");
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return new FileNameValidationResult(false, "invalid_file_name");
        }

        return ImageFormatRules.GetSupportedImageExtension(fileName) is null
            ? new FileNameValidationResult(false, "unsupported_extension")
            : new FileNameValidationResult(true, null);
    }

    public static DropTargetBatchRenamePlan PlanDropTargetBatchRename(
        IEnumerable<string> sourcePaths,
        string targetPath,
        Func<string, bool> targetExists)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(targetExists);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Target path must include a directory.", nameof(targetPath));
        var targetBaseName = Path.GetFileNameWithoutExtension(targetPath);

        var items = sourcePaths
            .Where(source => !PathEquals(source, targetPath))
            .Select((source, index) => CreatePlanItem(source, targetDirectory, targetBaseName, index + 1, targetExists))
            .ToList();

        return new DropTargetBatchRenamePlan(items.Count, items);
    }

    private static DropTargetBatchRenamePlanItem CreatePlanItem(
        string sourcePath,
        string targetDirectory,
        string targetBaseName,
        int sequenceNumber,
        Func<string, bool> targetExists)
    {
        var extension = Path.GetExtension(sourcePath);
        var targetPath = Path.Combine(targetDirectory, $"{targetBaseName}-{sequenceNumber:00}{extension}");

        if (IsAlreadyTargetSequence(sourcePath, targetBaseName))
        {
            return new DropTargetBatchRenamePlanItem(sourcePath, targetPath, true, AlreadyTargetSequenceReason);
        }

        if (targetExists(targetPath))
        {
            return new DropTargetBatchRenamePlanItem(sourcePath, targetPath, true, "target_exists");
        }

        return new DropTargetBatchRenamePlanItem(sourcePath, targetPath, false, null);
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsAlreadyTargetSequence(string sourcePath, string targetBaseName)
    {
        var sourceBaseName = Path.GetFileNameWithoutExtension(sourcePath);
        if (!sourceBaseName.StartsWith($"{targetBaseName}-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = sourceBaseName[(targetBaseName.Length + 1)..];
        return suffix.Length > 0 && suffix.All(char.IsDigit);
    }
}

public sealed record FileNameValidationResult(bool IsValid, string? Reason);

public sealed record DropTargetBatchRenamePlan(int Total, IReadOnlyList<DropTargetBatchRenamePlanItem> Items);

public sealed record DropTargetBatchRenamePlanItem(
    string SourcePath,
    string TargetPath,
    bool ShouldSkip,
    string? Reason);
