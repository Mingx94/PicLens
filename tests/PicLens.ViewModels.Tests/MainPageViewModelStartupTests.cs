using PicLens.Application.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.Diagnostics;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

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
    public async Task NavigateToFolderAsync_keeps_last_folder_from_picker_selection()
    {
        using var workspace = new TempDirectory();
        var childFolder = System.IO.Path.Combine(workspace.Path, "Child");
        Directory.CreateDirectory(childFolder);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault());
        var scanner = new CountingFolderScanner([]);
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () => Task.FromResult<string?>(workspace.Path));

        await viewModel.InitializeAsync();

        await viewModel.NavigateToFolderAsync(childFolder);

        Assert.Equal(childFolder, viewModel.CurrentFolderPath);
        Assert.Equal(workspace.Path, settingsStore.Settings.LastFolderPath);
    }

    [Fact]
    public async Task OpenFolderCommand_persists_picker_selection()
    {
        using var workspace = new TempDirectory();
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault());
        var scanner = new CountingFolderScanner([]);
        var viewModel = CreateViewModel(
            settingsStore,
            scanner,
            () => Task.FromResult<string?>(workspace.Path));

        await viewModel.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal(workspace.Path, viewModel.CurrentFolderPath);
        Assert.Equal(workspace.Path, settingsStore.Settings.LastFolderPath);
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

    [Fact]
    public async Task NavigateToFolderAsync_ignores_stale_scan_results_from_previous_folder()
    {
        using var firstWorkspace = new TempDirectory();
        using var secondWorkspace = new TempDirectory();
        var firstImage = new ImageListItem(
            "image:first",
            System.IO.Path.Combine(firstWorkspace.Path, "first.jpg"),
            "first.jpg",
            "jpg",
            100,
            1024);
        var secondImage = new ImageListItem(
            "image:second",
            System.IO.Path.Combine(secondWorkspace.Path, "second.jpg"),
            "second.jpg",
            "jpg",
            200,
            1024);
        var scanner = new ControllableFolderScanner();
        var viewModel = CreateViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            scanner,
            () => Task.FromResult<string?>(null));

        var firstNavigation = viewModel.NavigateToFolderAsync(firstWorkspace.Path);
        await scanner.WaitForScanAsync(firstWorkspace.Path);

        var secondNavigation = viewModel.NavigateToFolderAsync(secondWorkspace.Path);
        await scanner.WaitForScanAsync(secondWorkspace.Path);
        scanner.CompleteScan(secondWorkspace.Path, [secondImage]);
        await secondNavigation;

        Assert.Equal(secondWorkspace.Path, viewModel.CurrentFolderPath);
        Assert.Equal(["second.jpg"], viewModel.LibraryItems.Select(item => item.Name));

        scanner.CompleteScan(firstWorkspace.Path, [firstImage]);
        await firstNavigation;

        Assert.Equal(secondWorkspace.Path, viewModel.CurrentFolderPath);
        Assert.Equal(["second.jpg"], viewModel.LibraryItems.Select(item => item.Name));
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
            new TestDialogService(chooseFolderAsync: chooseFolderAsync),
            new NullNavigationService(),
            new ImmediateDispatcherService(),
            appLogger: appLogger);

    private sealed class ControllableFolderScanner : IFolderScanner
    {
        private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<ListItem>>> scans = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<IReadOnlyList<ListItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
            scans[query.FolderPath] = source;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
            }

            return source.Task;
        }

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderListItem>>([]);

        public Task WaitForScanAsync(string folderPath)
        {
            if (scans.ContainsKey(folderPath))
            {
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {
                while (!scans.ContainsKey(folderPath))
                {
                    await Task.Delay(10);
                }
            }).WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void CompleteScan(string folderPath, IReadOnlyList<ListItem> items)
        {
            scans[folderPath].TrySetResult(items);
        }
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

}
