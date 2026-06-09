using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using Microsoft.VisualBasic.FileIO;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ImageViewerWin.Infrastructure.Services;

public sealed class FileOperationService : IFileOperationService
{
    private static readonly HashSet<string> JpgExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg",
        "jpeg"
    };

    private readonly IJpegEncoder jpegEncoder;
    private readonly IRecycleBin recycleBin;

    public FileOperationService()
        : this(new WinRTJpegEncoder(), new WindowsRecycleBin())
    {
    }

    public FileOperationService(IJpegEncoder jpegEncoder, IRecycleBin recycleBin)
    {
        this.jpegEncoder = jpegEncoder;
        this.recycleBin = recycleBin;
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
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            await recycleBin.TrashAsync(path, cancellationToken);
            return new FileOperationResult(path, FileOperationStatus.Trashed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
        if (PathEquals(sourcePath, targetPath))
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
        var plan = FileRenamePlanner.PlanDropTargetBatchRename(sourcePaths, targetPath, File.Exists);
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
            await jpegEncoder.EncodeAsJpegAsync(image.Path, targetPath, cancellationToken);
            return new FileOperationResult(image.Path, FileOperationStatus.Converted, targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
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

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string BasenameKey(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var basename = Path.GetFileNameWithoutExtension(path);
        return $"{Path.GetFullPath(directory)}\0{basename}";
    }
}

public interface IJpegEncoder
{
    Task EncodeAsJpegAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default);
}

public interface IRecycleBin
{
    Task TrashAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class WinRTJpegEncoder : IJpegEncoder
{
    public async Task EncodeAsJpegAsync(string sourcePath, string targetPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken);
        using var inputStream = await sourceFile.OpenReadAsync().AsTask(cancellationToken);
        var decoder = await BitmapDecoder.CreateAsync(inputStream).AsTask(cancellationToken);

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("目標路徑必須包含資料夾。");
        var targetFolder = await StorageFolder.GetFolderFromPathAsync(targetDirectory).AsTask(cancellationToken);
        var targetFile = await targetFolder
            .CreateFileAsync(Path.GetFileName(targetPath), CreationCollisionOption.FailIfExists)
            .AsTask(cancellationToken);

        try
        {
            using IRandomAccessStream outputStream = await targetFile.OpenAsync(FileAccessMode.ReadWrite).AsTask(cancellationToken);
            var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream).AsTask(cancellationToken);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                decoder.PixelWidth,
                decoder.PixelHeight,
                decoder.DpiX,
                decoder.DpiY,
                pixelData.DetachPixelData());
            await encoder.FlushAsync().AsTask(cancellationToken);
        }
        catch
        {
            await targetFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask();
            throw;
        }
    }
}

public sealed class WindowsRecycleBin : IRecycleBin
{
    public async Task TrashAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
    }
}
