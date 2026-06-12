using PicLens.Core.Domain;

namespace PicLens.Application.Services;

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
        Func<string, string, bool> targetNameExists)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(targetNameExists);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Target path must include a directory.", nameof(targetPath));
        var targetBaseName = Path.GetFileNameWithoutExtension(targetPath);

        var items = new List<DropTargetBatchRenamePlanItem>();
        var sequenceNumber = 1;

        foreach (var source in sourcePaths.Where(source => !PathEquals(source, targetPath)))
        {
            var item = CreatePlanItem(source, targetDirectory, targetBaseName, sequenceNumber, targetNameExists);
            items.Add(item);
            sequenceNumber = Math.Max(sequenceNumber, ExtractSequenceNumber(item.TargetPath, targetBaseName) + 1);
        }

        return new DropTargetBatchRenamePlan(items.Count, items);
    }

    private static DropTargetBatchRenamePlanItem CreatePlanItem(
        string sourcePath,
        string targetDirectory,
        string targetBaseName,
        int sequenceNumber,
        Func<string, string, bool> targetNameExists)
    {
        var sourceSequenceNumber = TryExtractSequenceNumber(sourcePath, targetBaseName);
        if (sourceSequenceNumber is not null
            && sourceSequenceNumber.Value < sequenceNumber
            && !targetNameExists(sourcePath, sourcePath))
        {
            return new DropTargetBatchRenamePlanItem(sourcePath, sourcePath, true, AlreadyTargetSequenceReason);
        }

        var nextTargetPath = NextAvailableSequenceTargetPath(
            sourcePath,
            targetDirectory,
            targetBaseName,
            sequenceNumber,
            targetNameExists);
        return PathEquals(sourcePath, nextTargetPath)
            ? new DropTargetBatchRenamePlanItem(sourcePath, nextTargetPath, true, AlreadyTargetSequenceReason)
            : new DropTargetBatchRenamePlanItem(sourcePath, nextTargetPath, false, null);
    }

    private static string NextAvailableSequenceTargetPath(
        string sourcePath,
        string targetDirectory,
        string targetBaseName,
        int sequenceNumber,
        Func<string, string, bool> targetNameExists)
    {
        var candidateSequence = sequenceNumber;
        while (true)
        {
            var candidatePath = CreateSequenceTargetPath(sourcePath, targetDirectory, targetBaseName, candidateSequence);
            if (!targetNameExists(candidatePath, sourcePath))
            {
                return candidatePath;
            }

            candidateSequence += 1;
        }
    }

    private static string CreateSequenceTargetPath(
        string sourcePath,
        string targetDirectory,
        string targetBaseName,
        int sequenceNumber) =>
        Path.Combine(targetDirectory, $"{targetBaseName}-{sequenceNumber:00}{Path.GetExtension(sourcePath)}");

    private static int ExtractSequenceNumber(string targetPath, string targetBaseName)
    {
        return TryExtractSequenceNumber(targetPath, targetBaseName)
            ?? throw new ArgumentException("Target path must include a target sequence number.", nameof(targetPath));
    }

    private static int? TryExtractSequenceNumber(string targetPath, string targetBaseName)
    {
        var targetName = Path.GetFileNameWithoutExtension(targetPath);
        if (!targetName.StartsWith($"{targetBaseName}-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = targetName[(targetBaseName.Length + 1)..];
        return suffix.Length > 0 && suffix.All(char.IsDigit) && int.TryParse(suffix, out var sequenceNumber)
            ? sequenceNumber
            : null;
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

}

public sealed record FileNameValidationResult(bool IsValid, string? Reason);

public sealed record DropTargetBatchRenamePlan(int Total, IReadOnlyList<DropTargetBatchRenamePlanItem> Items);

public sealed record DropTargetBatchRenamePlanItem(
    string SourcePath,
    string TargetPath,
    bool ShouldSkip,
    string? Reason);
