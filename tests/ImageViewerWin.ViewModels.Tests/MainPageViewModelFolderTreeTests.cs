using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;
using ImageViewerWin.Diagnostics;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageViewModelFolderTreeTests
{
    [Fact]
    public async Task NavigateToFolderAsync_keeps_tree_root_and_selects_current_child()
    {
        using var workspace = new TempDirectory();
        var childFolder = Directory.CreateDirectory(Path.Combine(workspace.Path, "Child")).FullName;
        var scanner = new FolderTreeScanner(
            [],
            new Dictionary<string, IReadOnlyList<FolderListItem>>(PathComparer)
            {
                [workspace.Path] = [Folder(childFolder)]
            });
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            scanner);

        await viewModel.InitializeAsync();
        await viewModel.NavigateToFolderAsync(childFolder);

        var root = Assert.Single(viewModel.FolderRoots);
        Assert.Equal(workspace.Path, root.Path);
        Assert.True(root.IsExpanded);
        Assert.False(root.IsSelected);

        var child = Assert.Single(root.Children);
        Assert.Equal(childFolder, child.Path);
        Assert.True(child.IsExpanded);
        Assert.True(child.IsSelected);
    }

    [Fact]
    public async Task OpenFolderCommand_resets_tree_root_to_picker_selection()
    {
        using var firstWorkspace = new TempDirectory();
        using var secondWorkspace = new TempDirectory();
        var firstChild = Directory.CreateDirectory(Path.Combine(firstWorkspace.Path, "Child")).FullName;
        var scanner = new FolderTreeScanner(
            [],
            new Dictionary<string, IReadOnlyList<FolderListItem>>(PathComparer)
            {
                [firstWorkspace.Path] = [Folder(firstChild)]
            });
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = firstWorkspace.Path },
            scanner,
            () => Task.FromResult<string?>(secondWorkspace.Path));

        await viewModel.InitializeAsync();
        await viewModel.NavigateToFolderAsync(firstChild);
        await viewModel.OpenFolderCommand.ExecuteAsync(null);

        var root = Assert.Single(viewModel.FolderRoots);
        Assert.Equal(secondWorkspace.Path, root.Path);
        Assert.True(root.IsExpanded);
        Assert.True(root.IsSelected);
    }

    [Fact]
    public async Task BackAndForward_restore_tree_root_for_history_entry()
    {
        using var firstWorkspace = new TempDirectory();
        using var secondWorkspace = new TempDirectory();
        var firstChild = Directory.CreateDirectory(Path.Combine(firstWorkspace.Path, "Child")).FullName;
        var scanner = new FolderTreeScanner(
            [],
            new Dictionary<string, IReadOnlyList<FolderListItem>>(PathComparer)
            {
                [firstWorkspace.Path] = [Folder(firstChild)]
            });
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = firstWorkspace.Path },
            scanner,
            () => Task.FromResult<string?>(secondWorkspace.Path));

        await viewModel.InitializeAsync();
        await viewModel.NavigateToFolderAsync(firstChild);
        await viewModel.OpenFolderCommand.ExecuteAsync(null);

        await viewModel.BackCommand.ExecuteAsync(null);
        var firstRoot = Assert.Single(viewModel.FolderRoots);
        Assert.Equal(firstWorkspace.Path, firstRoot.Path);
        Assert.True(Assert.Single(firstRoot.Children).IsSelected);

        await viewModel.ForwardCommand.ExecuteAsync(null);
        var secondRoot = Assert.Single(viewModel.FolderRoots);
        Assert.Equal(secondWorkspace.Path, secondRoot.Path);
        Assert.True(secondRoot.IsSelected);
    }

    [Fact]
    public async Task LoadFolderTreeAsync_logs_child_scan_failures_and_keeps_root()
    {
        using var workspace = new TempDirectory();
        var expected = new UnauthorizedAccessException("blocked");
        var scanner = new ThrowingChildFolderScanner(expected);
        var logger = new RecordingAppLogger();
        var viewModel = CreateViewModel(
            AppSettings.CreateDefault() with { LastFolderPath = workspace.Path },
            scanner,
            appLogger: logger);

        await viewModel.InitializeAsync();

        var root = Assert.Single(viewModel.FolderRoots);
        Assert.Equal(workspace.Path, root.Path);
        Assert.True(root.IsExpanded);
        Assert.True(root.IsSelected);
        var entry = Assert.Single(logger.ErrorMessages);
        Assert.Same(expected, entry.Exception);
        Assert.Contains($"FolderPath={workspace.Path}", entry.Message);
        Assert.Contains($"CurrentFolderPath={workspace.Path}", entry.Message);
    }

    private static MainPageViewModel CreateViewModel(
        AppSettings settings,
        IFolderScanner scanner,
        Func<Task<string?>>? chooseFolderAsync = null,
        IAppLogger? appLogger = null) =>
        new(
            new FakeSettingsStore(settings),
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            chooseFolderAsync ?? (() => Task.FromResult<string?>(null)),
            (_, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { },
            appLogger: appLogger);

    private static FolderListItem Folder(string path) =>
        new($"folder:{path}", path, Path.GetFileName(path), 0);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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

    private sealed class FolderTreeScanner(
        IReadOnlyList<ListItem> items,
        IReadOnlyDictionary<string, IReadOnlyList<FolderListItem>> childFolders) : IFolderScanner
    {
        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(ListItemSorter.Sort(items, query.Sort, new SortOptions(KeepFoldersFirst: true)));

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default)
        {
            childFolders.TryGetValue(folderPath, out var folders);
            return Task.FromResult(folders ?? []);
        }
    }

    private sealed class ThrowingChildFolderScanner(Exception exception) : IFolderScanner
    {
        public Task<IReadOnlyList<ListItem>> ScanAsync(ListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ListItem>>([]);

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<(Exception Exception, string Message)> ErrorMessages { get; } = [];

        public void Info(string message)
        {
        }

        public void Error(Exception exception, string message) => ErrorMessages.Add((exception, message));
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
