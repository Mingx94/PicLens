using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.Diagnostics;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageViewModelStartupTests
{
    [Fact]
    public async Task InitializeAsync_restores_valid_last_folder_without_opening_picker()
    {
        using var workspace = new TempDirectory();
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var scanner = new CountingFolderScanner([]);
        var pickerCalls = 0;
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () =>
            {
                pickerCalls += 1;
                return Task.FromResult<string?>(workspace.Path);
            });

        await viewModel.InitializeAsync();

        Assert.Equal(0, pickerCalls);
        Assert.Equal(workspace.Path, viewModel.CurrentFolderPath);
        Assert.Equal(1, scanner.ScanCount);
    }

    [Fact]
    public async Task InitializeAsync_without_valid_last_folder_asks_for_folder_and_persists_selection()
    {
        using var workspace = new TempDirectory();
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault());
        var scanner = new CountingFolderScanner([]);
        var pickerCalls = 0;
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () =>
            {
                pickerCalls += 1;
                return Task.FromResult<string?>(workspace.Path);
            });

        await viewModel.InitializeAsync();

        Assert.Equal(1, pickerCalls);
        Assert.Equal(workspace.Path, viewModel.CurrentFolderPath);
        Assert.Equal(workspace.Path, settingsStore.Settings.LastFolderPath);
        Assert.Equal(1, scanner.ScanCount);
    }

    [Fact]
    public async Task InitializeAsync_with_cancelled_folder_picker_leaves_library_empty()
    {
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault());
        var scanner = new CountingFolderScanner([]);
        var pickerCalls = 0;
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () =>
            {
                pickerCalls += 1;
                return Task.FromResult<string?>(null);
            });

        await viewModel.InitializeAsync();

        Assert.Equal(1, pickerCalls);
        Assert.Equal(string.Empty, viewModel.CurrentFolderPath);
        Assert.Empty(viewModel.LibraryItems);
        Assert.Equal(0, scanner.ScanCount);
    }

    [Fact]
    public async Task IncludeSubfolders_change_logs_background_reload_failures()
    {
        using var workspace = new TempDirectory();
        var expected = new InvalidOperationException("settings write failed");
        var settingsStore = new ThrowingUpdateSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        }, expected);
        var scanner = new CountingFolderScanner([]);
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () => Task.FromResult<string?>(null),
            logger);

        await viewModel.InitializeAsync();

        viewModel.IncludeSubfolders = true;
        var entry = await logger.WaitForErrorAsync();

        Assert.Equal("Toggle include subfolders failed.", entry.Message);
        Assert.Same(expected, entry.Exception);
        Assert.Contains("已寫入診斷記錄", viewModel.StatusMessage);
    }

    private static MainPageViewModel CreateViewModel(
        ISettingsStore settingsStore,
        IFolderScanner scanner,
        Func<Task<string?>> chooseFolderAsync,
        IAppLogger? appLogger = null) =>
        new(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            chooseFolderAsync,
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { },
            appLogger: appLogger);

    private sealed class CountingFolderScanner(IReadOnlyList<ListItem> items) : IFolderScanner
    {
        public int ScanCount { get; private set; }

        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
        {
            ScanCount += 1;
            return Task.FromResult(ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: true)));
        }

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderListItem>>([]);
    }

    private sealed class FakeSettingsStore(AppSettings initialSettings) : ISettingsStore
    {
        public AppSettings Settings { get; private set; } = initialSettings;

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

    private sealed class ThrowingUpdateSettingsStore(AppSettings initialSettings, Exception exception) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(initialSettings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            throw exception;

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        private readonly TaskCompletionSource<Entry> errorLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Entry> WaitForErrorAsync() => errorLogged.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Info(string message)
        {
        }

        public void Error(Exception exception, string message)
        {
            errorLogged.TrySetResult(new Entry(exception, message));
        }

        public sealed record Entry(Exception Exception, string Message);
    }

    private sealed class ThrowingFileOperationService : IFileOperationService
    {
        public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationBatchResult> TrashSameBasenameNonJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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

    private sealed class NullThumbnailService : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ImageViewerWin.ViewModels.Tests",
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
}
