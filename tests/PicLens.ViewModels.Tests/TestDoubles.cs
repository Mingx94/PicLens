using PicLens.Application.Services;
using PicLens.Core.Models;
using PicLens.Diagnostics;

namespace PicLens.ViewModels.Tests;

internal sealed class ThrowingFileOperationService(Exception? exception = null) : IFileOperationService
{
    public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
        IEnumerable<ImageListItem> visibleImages,
        CancellationToken cancellationToken = default) =>
        throw exception ?? new NotSupportedException();

    public Task<FileOperationBatchResult> TrashSameBasenameNonJpgAsync(
        IEnumerable<ImageListItem> visibleImages,
        CancellationToken cancellationToken = default) =>
        throw exception ?? new NotSupportedException();

    public Task<FileOperationResult> TrashAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<FileOperationResult> RenameAsync(
        string sourcePath,
        string newFileName,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<FileOperationBatchResult> RenameByDropTargetAsync(
        IEnumerable<string> sourcePaths,
        string targetPath,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class NullThumbnailService : IThumbnailService
{
    public Task<string?> GetOrCreateThumbnailAsync(
        string imagePath,
        int requestedSize,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

internal sealed class RecordingAppLogger : IAppLogger
{
    private readonly TaskCompletionSource<Entry> errorLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<string> InfoMessages { get; } = [];

    public List<Entry> ErrorMessages { get; } = [];

    public Task<Entry> WaitForErrorAsync() => errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public void Info(string message) => InfoMessages.Add(message);

    public void Error(Exception exception, string message)
    {
        var entry = new Entry(exception, message);
        ErrorMessages.Add(entry);
        errorLogged.TrySetResult(entry);
    }

    public sealed record Entry(Exception Exception, string Message);
}
