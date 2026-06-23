using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Diagnostics;
using PicLens.Services;

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

internal sealed class TestThumbnailService(
    Func<string, int, CancellationToken, Task<string?>>? getOrCreateThumbnailAsync = null) : IThumbnailService
{
    public List<(string SourcePath, int RequestedSize)> Requests { get; } = [];

    public Task<string?> GetOrCreateThumbnailAsync(
        string imagePath,
        int requestedSize,
        CancellationToken cancellationToken = default)
    {
        Requests.Add((imagePath, requestedSize));
        return getOrCreateThumbnailAsync?.Invoke(imagePath, requestedSize, cancellationToken)
            ?? Task.FromResult<string?>(null);
    }
}

internal sealed class CountingFolderScanner(IReadOnlyList<ListItem> items) : IFolderScanner
{
    public int ScanCount { get; private set; }

    public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
    {
        ScanCount += 1;
        return Task.FromResult(ListItemSorter.Sort(items, query.Sort, keepFoldersFirst: true));
    }

    public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
        string folderPath,
        SortState sort,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FolderListItem>>([]);
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

internal sealed class NullDialogService : IDialogService
{
    public Task<string?> ChooseFolderAsync() => Task.FromResult<string?>(null);

    public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText) => Task.FromResult(false);

    public Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview) => Task.FromResult(false);

    public Task<string?> RequestRenameAsync(ImageListItem item) => Task.FromResult<string?>(null);
}

internal sealed class TestDialogService(
    Func<Task<string?>>? chooseFolderAsync = null,
    Func<string, string, string, Task<bool>>? confirmAsync = null,
    Func<DropRenamePreview, Task<bool>>? confirmDropRenameAsync = null,
    Func<ImageListItem, Task<string?>>? requestRenameAsync = null) : IDialogService
{
    public Task<string?> ChooseFolderAsync() =>
        chooseFolderAsync?.Invoke() ?? Task.FromResult<string?>(null);

    public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText) =>
        confirmAsync?.Invoke(message, title, confirmButtonText) ?? Task.FromResult(false);

    public Task<bool> ConfirmDropRenameAsync(DropRenamePreview preview) =>
        confirmDropRenameAsync?.Invoke(preview) ?? Task.FromResult(false);

    public Task<string?> RequestRenameAsync(ImageListItem item) =>
        requestRenameAsync?.Invoke(item) ?? Task.FromResult<string?>(null);
}

internal sealed class FakeSettingsStore(AppSettings initialSettings) : ISettingsStore
{
    public AppSettings Settings { get; private set; } = initialSettings;

    public AppSettings Current => Settings;

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }

    public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        Settings = SettingsRules.MergeSettingsPatch(Settings, patch);
        return Task.FromResult(Settings);
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "PicLens.ViewModels.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
