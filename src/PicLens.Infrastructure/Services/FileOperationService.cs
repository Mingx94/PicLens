using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using Microsoft.VisualBasic.FileIO;
using System.ComponentModel;
using System.Diagnostics;
using SkiaSharp;

namespace PicLens.Infrastructure.Services;

public sealed class FileOperationService : IFileOperationService
{
    private static readonly HashSet<string> JpgExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg",
        "jpeg"
    };

    private readonly Func<string, string, CancellationToken, Task> encodeAsJpegAsync;
    private readonly Func<string, CancellationToken, Task> trashAsync;

    public FileOperationService()
        : this(EncodeAsJpegAsync, TrashPathAsync)
    {
    }

    public FileOperationService(
        Func<string, string, CancellationToken, Task> encodeAsJpegAsync,
        Func<string, CancellationToken, Task> trashAsync)
    {
        this.encodeAsJpegAsync = encodeAsJpegAsync;
        this.trashAsync = trashAsync;
    }

    public async Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
        IEnumerable<ImageListItem> visibleImages,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FileOperationResult>();

        foreach (var image in visibleImages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ConvertOneToJpgAsync(image, cancellationToken));
        }

        return ToBatchResult(results);
    }

    public async Task<FileOperationBatchResult> TrashSameBasenameNonJpgAsync(
        IEnumerable<ImageListItem> visibleImages,
        CancellationToken cancellationToken = default)
    {
        var images = visibleImages.ToList();
        var jpgBasenames = images
            .Where(image => JpgExtensions.Contains(image.Extension))
            .Select(image => BasenameKey(image.Path))
            .ToHashSet(PathRules.PathComparer);
        var results = new List<FileOperationResult>();

        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (JpgExtensions.Contains(image.Extension))
            {
                results.Add(new FileOperationResult(image.Path, FileOperationStatus.Skipped, Reason: "already_jpg"));
                continue;
            }

            if (!jpgBasenames.Contains(BasenameKey(image.Path)))
            {
                results.Add(new FileOperationResult(image.Path, FileOperationStatus.Skipped, Reason: "no_matching_jpg"));
                continue;
            }

            results.Add(await TrashAsync(image.Path, cancellationToken));
        }

        return ToBatchResult(results);
    }

    public async Task<FileOperationResult> TrashAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return new FileOperationResult(path, FileOperationStatus.Failed, Reason: "source_missing");
        }

        try
        {
            await trashAsync(path, cancellationToken);
            return new FileOperationResult(path, FileOperationStatus.Trashed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FileOperationResult(path, FileOperationStatus.Failed, Reason: "trash_failed", Message: ex.Message);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return new FileOperationResult(path, FileOperationStatus.Failed, Reason: "trash_failed", Message: ex.Message);
        }
    }

    public Task<FileOperationResult> RenameAsync(
        string sourcePath,
        string newFileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = FileRenamePlanner.ValidateImageFileName(newFileName);
        if (!validation.IsValid)
        {
            return Task.FromResult(ToInvalidRenameRequest(sourcePath, validation.Reason));
        }

        if (ImageFormatRules.GetSupportedImageExtension(sourcePath) is null)
        {
            return Task.FromResult(new FileOperationResult(
                sourcePath,
                FileOperationStatus.Failed,
                Reason: "invalid_request",
                Message: "路徑必須指向支援的圖片檔案。"));
        }

        if (!File.Exists(sourcePath))
        {
            return Task.FromResult(new FileOperationResult(sourcePath, FileOperationStatus.Failed, Reason: "source_missing"));
        }

        var directory = Path.GetDirectoryName(sourcePath)!;
        var targetPath = Path.Combine(directory, newFileName);
        if (PathRules.PathEquals(sourcePath, targetPath))
        {
            return Task.FromResult(new FileOperationResult(sourcePath, FileOperationStatus.Skipped, targetPath, "same_name"));
        }

        if (File.Exists(targetPath))
        {
            return Task.FromResult(new FileOperationResult(
                sourcePath,
                FileOperationStatus.Failed,
                targetPath,
                "invalid_request",
                "已有相同名稱的檔案。"));
        }

        try
        {
            File.Move(sourcePath, targetPath);
            return Task.FromResult(new FileOperationResult(sourcePath, FileOperationStatus.Renamed, targetPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(new FileOperationResult(sourcePath, FileOperationStatus.Failed, targetPath, "rename_failed", ex.Message));
        }
    }

    public async Task<FileOperationBatchResult> RenameByDropTargetAsync(
        IEnumerable<string> sourcePaths,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        var plan = FileRenamePlanner.PlanDropTargetBatchRename(
            sourcePaths,
            targetPath,
            ExistingTargetDirectoryFiles(targetPath));
        var results = new List<FileOperationResult>();

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ShouldSkip)
            {
                results.Add(new FileOperationResult(item.SourcePath, FileOperationStatus.Skipped, item.TargetPath, item.Reason));
                continue;
            }

            if (!File.Exists(item.SourcePath))
            {
                results.Add(new FileOperationResult(item.SourcePath, FileOperationStatus.Failed, item.TargetPath, "source_missing"));
                continue;
            }

            try
            {
                File.Move(item.SourcePath, item.TargetPath);
                results.Add(new FileOperationResult(item.SourcePath, FileOperationStatus.Renamed, item.TargetPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(new FileOperationResult(item.SourcePath, FileOperationStatus.Failed, item.TargetPath, "rename_failed", ex.Message));
            }
        }

        return ToBatchResult(results);
    }

    private async Task<FileOperationResult> ConvertOneToJpgAsync(ImageListItem image, CancellationToken cancellationToken)
    {
        if (!File.Exists(image.Path))
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Failed, Reason: "source_missing");
        }

        if (JpgExtensions.Contains(image.Extension))
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Skipped, Reason: "already_jpg");
        }

        if (image.IsAnimated)
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Skipped, Reason: "animated_unsupported");
        }

        if (ImageFormatRules.GetSupportedImageExtension(image.Path) is null)
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Skipped, Reason: "unsupported_extension");
        }

        var targetPath = Path.Combine(
            Path.GetDirectoryName(image.Path)!,
            $"{Path.GetFileNameWithoutExtension(image.Path)}.jpg");

        if (File.Exists(targetPath))
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Skipped, targetPath, "target_exists");
        }

        try
        {
            await encodeAsJpegAsync(image.Path, targetPath, cancellationToken);
            return new FileOperationResult(image.Path, FileOperationStatus.Converted, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new FileOperationResult(image.Path, FileOperationStatus.Failed, targetPath, "conversion_failed", ex.Message);
        }
    }

    private static FileOperationBatchResult ToBatchResult(IReadOnlyList<FileOperationResult> results) =>
        new(
            Total: results.Count,
            Succeeded: results.Count(item => item.Status is FileOperationStatus.Converted or FileOperationStatus.Trashed or FileOperationStatus.Renamed),
            Skipped: results.Count(item => item.Status == FileOperationStatus.Skipped),
            Failed: results.Count(item => item.Status == FileOperationStatus.Failed),
            Items: results);

    private static FileOperationResult ToInvalidRenameRequest(string sourcePath, string? validationReason) =>
        new(
            sourcePath,
            FileOperationStatus.Failed,
            Reason: "invalid_request",
            Message: validationReason == "unsupported_extension"
                ? "檔名必須使用支援的圖片副檔名。"
                : "檔名必須是不含路徑分隔符號的單一檔名。");

    private static IReadOnlyList<string> ExistingTargetDirectoryFiles(string targetPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("目標路徑必須包含資料夾。");
        return Directory.Exists(targetDirectory)
            ? Directory.EnumerateFiles(targetDirectory).ToList()
            : [];
    }

    private static string BasenameKey(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var basename = Path.GetFileNameWithoutExtension(path);
        return $"{Path.GetFullPath(directory)}\0{basename}";
    }

    private static async Task EncodeAsJpegAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var input = File.OpenRead(sourcePath);
            using var bitmap = SKBitmap.Decode(input)
                ?? throw new NotSupportedException("Image could not be decoded.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var output = File.Create(targetPath);
                if (!bitmap.Encode(output, SKEncodedImageFormat.Jpeg, quality: 90))
                {
                    throw new NotSupportedException("JPEG could not be encoded.");
                }
            }
            catch
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                throw;
            }
        }, cancellationToken);
    }

    private static async Task TrashPathAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() =>
            {
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
            }, cancellationToken);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await RunProcessAsync("gio", ["trash", path], cancellationToken);
            return;
        }

        throw new PlatformNotSupportedException("Trash is only supported on Windows and Linux.");
    }

    private static async Task RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"{fileName} could not be started.");
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new IOException($"{fileName} failed with exit code {process.ExitCode}.");
        }
    }
}
