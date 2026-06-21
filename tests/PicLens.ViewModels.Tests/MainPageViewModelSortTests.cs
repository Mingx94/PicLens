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
            new NullDialogService());

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
            new NullDialogService());

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

    [Theory]
    [InlineData("name-asc", SortKey.Name, SortDirection.Asc, "alpha.jpg", "zulu.jpg")]
    [InlineData("name-desc", SortKey.Name, SortDirection.Desc, "zulu.jpg", "alpha.jpg")]
    [InlineData("modified-asc", SortKey.ModifiedAt, SortDirection.Asc, "zulu.jpg", "alpha.jpg")]
    [InlineData("modified-desc", SortKey.ModifiedAt, SortDirection.Desc, "alpha.jpg", "zulu.jpg")]
    public async Task ChangeSortOptionCommand_applies_sort_token_without_rescanning(
        string token,
        SortKey expectedKey,
        SortDirection expectedDirection,
        string firstName,
        string secondName)
    {
        using var workspace = new TempDirectory();
        var scanner = new CountingFolderScanner(
        [
            new ImageListItem("image:zulu", System.IO.Path.Combine(workspace.Path, "zulu.jpg"), "zulu.jpg", ".jpg", 100, 1024),
            new ImageListItem("image:alpha", System.IO.Path.Combine(workspace.Path, "alpha.jpg"), "alpha.jpg", ".jpg", 200, 1024)
        ]);
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault() with
            {
                LastFolderPath = workspace.Path
            }),
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());

        await viewModel.InitializeAsync();
        var scansAfterInitialize = scanner.ScanCount;

        await viewModel.ChangeSortOptionCommand.ExecuteAsync(token);

        Assert.Equal(scansAfterInitialize, scanner.ScanCount);
        Assert.Equal(new SortState(expectedKey, expectedDirection), viewModel.Sort);
        Assert.Equal([firstName, secondName], viewModel.LibraryItems.Select(item => item.Name));
    }

    [Fact]
    public async Task ChangeSortOptionCommand_logs_invalid_tokens_without_changing_sort()
    {
        using var workspace = new TempDirectory();
        var logger = new RecordingAppLogger();
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault() with
            {
                LastFolderPath = workspace.Path
            }),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService(),
            appLogger: logger);

        await viewModel.InitializeAsync();
        var originalSort = viewModel.Sort;

        await viewModel.ChangeSortOptionCommand.ExecuteAsync("bad-token");

        Assert.Equal(originalSort, viewModel.Sort);
        Assert.Contains("排序時發生錯誤", viewModel.StatusMessage);
        var entry = Assert.Single(logger.ErrorMessages);
        Assert.Contains("Change sort option failed.", entry.Message);
        Assert.Contains("bad-token", entry.Message);
    }

    [Fact]
    public async Task ToggleIncludeSubfoldersCommand_persists_and_reloads()
    {
        using var workspace = new TempDirectory();
        var settingsStore = new FakeSettingsStore(AppSettings.CreateDefault() with
        {
            LastFolderPath = workspace.Path
        });
        var scanner = new CountingFolderScanner([]);
        var viewModel = new MainPageViewModel(
            settingsStore,
            scanner,
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());

        await viewModel.InitializeAsync();
        var scansAfterInitialize = scanner.ScanCount;

        viewModel.ToggleIncludeSubfoldersCommand.Execute(null);
        await WaitUntilAsync(() => scanner.ScanCount > scansAfterInitialize);

        Assert.True(viewModel.IncludeSubfolders);
        Assert.True(settingsStore.Current.IncludeSubfolders);
    }

    [Fact]
    public void SortOptionsExposeKeyDirectionLabels()
    {
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService());

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
            new NullDialogService())
        {
            Sort = new SortState((SortKey)999, (SortDirection)999)
        };

        Assert.Equal("名稱由小到大", viewModel.SelectedSortOption.Label);
    }


    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

}
