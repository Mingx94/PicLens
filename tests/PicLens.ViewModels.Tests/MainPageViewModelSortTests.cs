using PicLens.Application.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using PicLens.ViewModels;

namespace PicLens.ViewModels.Tests;

public sealed class MainPageViewModelSortTests
{
    [Fact]
    public async Task ToggleSortDirectionReordersCurrentItemsWithoutRescanning()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:zulu", System.IO.Path.Combine(workspace.Path, "zulu.jpg"), "zulu.jpg", ".jpg", 200, 1024),
            new ImageListItem("image:alpha", System.IO.Path.Combine(workspace.Path, "alpha.jpg"), "alpha.jpg", ".jpg", 100, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var viewModel = new MainPageViewModel(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        await viewModel.InitializeAsync();
        var scansAfterInitialize = scanner.ScanCount;
        var initialOrder = viewModel.LibraryItems.Select(item => item.Name).ToArray();

        await viewModel.ToggleSortDirectionCommand.ExecuteAsync(null);

        Assert.Equal(scansAfterInitialize, scanner.ScanCount);
        Assert.Equal(["alpha.jpg", "zulu.jpg"], initialOrder);
        Assert.Equal(["zulu.jpg", "alpha.jpg"], viewModel.LibraryItems.Select(item => item.Name));
    }

    [Fact]
    public async Task ChangeSortAppliesCombinedSortOptionWithoutRescanning()
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:older", System.IO.Path.Combine(workspace.Path, "older.jpg"), "older.jpg", ".jpg", 100, 1024),
            new ImageListItem("image:newer", System.IO.Path.Combine(workspace.Path, "newer.jpg"), "newer.jpg", ".jpg", 200, 1024)
        ]);
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var viewModel = new MainPageViewModel(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        await viewModel.InitializeAsync();
        var scansAfterInitialize = scanner.ScanCount;
        var option = Assert.Single(viewModel.SortOptions, option => option.Label == "修改時間最新到最舊");

        await viewModel.ChangeSortAsync(option.State);

        Assert.Equal(scansAfterInitialize, scanner.ScanCount);
        Assert.Equal(new SortState(SortKey.ModifiedAt, SortDirection.Desc), viewModel.Sort);
        Assert.Equal("修改時間最新到最舊", viewModel.SortLabel);
        Assert.Equal("修改時間最新到最舊", viewModel.SelectedSortOption.Label);
        Assert.Equal(["newer.jpg", "older.jpg"], viewModel.LibraryItems.Select(item => item.Name));
        Assert.Equal(new SortState(SortKey.ModifiedAt, SortDirection.Desc), settingsStore.Current.Sort);
    }

    [Fact]
    public void SortOptionsExposeKeyDirectionLabels()
    {
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

        Assert.Equal(
            ["名稱由小到大", "名稱由大到小", "修改時間最舊到最新", "修改時間最新到最舊"],
            viewModel.SortOptions.Select(option => option.Label));
        Assert.Equal("名稱由小到大", viewModel.SelectedSortOption.Label);
    }

    [Fact]
    public void SelectedSortOptionFallsBackToDefaultWhenSortIsUnknown()
    {
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { })
        {
            Sort = new SortState((SortKey)999, (SortDirection)999)
        };

        Assert.Equal("名稱由小到大", viewModel.SelectedSortOption.Label);
    }

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
        private AppSettings settings = initialSettings;

        public AppSettings Current => settings;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            this.settings = settings;
            return Task.CompletedTask;
        }

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
        {
            settings = SettingsRules.MergeSettingsPatch(settings, patch);
            return Task.FromResult(settings);
        }
    }

    private sealed class TempDirectory : IDisposable
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
}
