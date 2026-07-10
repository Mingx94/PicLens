using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PicLens;
using AppSettings = PicLens.Core.Models.AppSettings;
using SortDirection = PicLens.Core.Models.SortDirection;
using SortKey = PicLens.Core.Models.SortKey;
using PicLens.Infrastructure.Services;
using PicLens.ViewModels;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(PicLens.Ui.Tests.TestAppBuilder))]

namespace PicLens.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .LogToTrace();
}

public sealed class MainWindowSmokeTests
{
    private static readonly string[] MainWindowAutomationIds =
    [
        "AppTitleBar",
        "TitleBarSidebarToggleButton",
        "TitleBarOpenFolderButton",
        "TitleBarSortMenuButton",
        "TitleBarRecursiveModeToggle",
        "TitleBarGridViewButton",
        "TitleBarListViewButton",
        "TitleBarMoreActionsButton",
        "LibrarySearchBox",
        "FolderTree",
        "LibraryGrid",
        "StatusInfoBar",
        "ThumbnailSizeSlider",
        "EmptyStateOpenFolderButton"
    ];

    [AvaloniaFact]
    public async Task Main_window_exposes_primary_controls_empty_state_and_menus()
    {
        using var fixture = PicLensHeadlessFixture.StartEmpty(nameof(Main_window_exposes_primary_controls_empty_state_and_menus));
        await fixture.WaitForConditionAsync(() => fixture.View.ViewModel.HasNoCurrentFolder, "empty state did not load");

        Assert.Equal("PicLens", fixture.Window.Title);
        Assert.Equal(ThemeVariant.Light, fixture.Window.ActualThemeVariant);
        Assert.Equal("Noto Sans CJK TC", TextElement.GetFontFamily(fixture.Window).Name);
        foreach (var automationId in MainWindowAutomationIds)
        {
            Assert.NotNull(fixture.FindByAutomationId<Control>(automationId));
        }

        var thumbnailSlider = fixture.FindByAutomationId<Slider>("ThumbnailSizeSlider");
        Assert.Equal(120, thumbnailSlider.Minimum);
        Assert.Equal(240, thumbnailSlider.Maximum);
        Assert.Equal(200, thumbnailSlider.Value);

        Assert.Equal(
            ["名稱由小到大", "名稱由大到小", "修改時間最舊到最新", "修改時間最新到最舊"],
            fixture.MenuHeaders("TitleBarSortMenuButton"));
        Assert.Equal(
            ["將目前顯示項目轉為 JPG", "清除同名非 JPG 檔案"],
            fixture.MenuHeaders("TitleBarMoreActionsButton"));

        foreach (var automationId in new[]
                 {
                     "TitleBarSidebarToggleButton",
                     "TitleBarBackButton",
                     "TitleBarForwardButton",
                     "TitleBarRefreshLibraryButton",
                     "TitleBarGridViewButton",
                     "TitleBarListViewButton",
                     "TitleBarMoreActionsButton",
                     "ThumbnailSizeSlider"
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(
                AutomationProperties.GetName(fixture.FindByAutomationId<Control>(automationId))));
        }
    }

    [AvaloniaFact]
    public async Task Thumbnail_slider_thumb_is_centered_without_footer_clipping()
    {
        using var fixture = PicLensHeadlessFixture.StartEmpty(nameof(Thumbnail_slider_thumb_is_centered_without_footer_clipping));
        await fixture.WaitForConditionAsync(() => fixture.View.ViewModel.HasNoCurrentFolder, "empty state did not load");

        var slider = fixture.FindByAutomationId<Slider>("ThumbnailSizeSlider");
        var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
        var footer = slider.GetVisualAncestors()
            .OfType<Border>()
            .First(border => Grid.GetRow(border) == 2);
        var thumbTopLeft = thumb.TranslatePoint(new Point(), footer);

        Assert.NotNull(thumbTopLeft);
        Assert.True(thumbTopLeft.Value.Y >= 0, "Slider thumb was clipped above the status bar.");
        Assert.True(
            thumbTopLeft.Value.Y + thumb.Bounds.Height <= footer.Bounds.Height,
            "Slider thumb was clipped below the status bar.");
        Assert.InRange(
            Math.Abs(thumbTopLeft.Value.Y + thumb.Bounds.Height / 2 - footer.Bounds.Height / 2),
            0,
            1);
    }

    [AvaloniaFact]
    public async Task Button_hover_states_use_component_specific_colors()
    {
        using var fixture = PicLensHeadlessFixture.StartEmpty(nameof(Button_hover_states_use_component_specific_colors));
        await fixture.WaitForConditionAsync(() => fixture.View.ViewModel.HasNoCurrentFolder, "empty state did not load");

        Assert.Equal(Color.Parse("#3F5DD3"), fixture.HoverButtonBackground("TitleBarOpenFolderButton"));
        Assert.Equal(Color.Parse("#ECEFF4"), fixture.HoverButtonBackground("TitleBarSortMenuButton"));
        Assert.Equal(Color.Parse("#E7EEFF"), fixture.HoverButtonBackground("TitleBarGridViewButton"));
        Assert.Equal(Color.Parse("#FFFFFF"), fixture.HoverButtonBackground("TitleBarListViewButton"));
        Assert.Equal(Color.Parse("#344FC4"), fixture.PressedButtonBackground("TitleBarOpenFolderButton"));
        Assert.Equal(Color.Parse("#E7EEFF"), fixture.PressedButtonBackground("TitleBarSortMenuButton"));
        Assert.Equal(Color.Parse("#DCE4FF"), fixture.PressedButtonBackground("TitleBarGridViewButton"));

        fixture.View.ViewModel.IncludeSubfolders = true;
        await fixture.WaitForConditionAsync(() => fixture.View.ViewModel.IncludeSubfolders, "scope toggle did not activate");
        Assert.Equal(Color.Parse("#E7EEFF"), fixture.HoverButtonBackground("TitleBarRecursiveModeToggle"));
        Assert.Equal(Color.Parse("#DCE4FF"), fixture.PressedButtonBackground("TitleBarRecursiveModeToggle"));
    }

    [AvaloniaFact]
    public async Task Seeded_library_loads_folder_tree_grid_status_and_thumbnails()
    {
        using var fixture = PicLensHeadlessFixture.StartSeeded(nameof(Seeded_library_loads_folder_tree_grid_status_and_thumbnails));
        await fixture.WaitForLibraryCountAsync(3);
        Assert.Contains("載入 3 個項目", fixture.View.ViewModel.StatusMessage, StringComparison.Ordinal);

        var longName = "8s_[8K] 251031 아이브 장원영 IVE WONYOUNG fancam very long original filename.png";
        File.Copy(Path.Combine(fixture.LibraryRoot, "Alpha-01.png"), Path.Combine(fixture.LibraryRoot, longName));
        fixture.ExecuteButtonCommand("TitleBarRefreshLibraryButton");
        await fixture.WaitForLibraryCountAsync(4);

        var alphaTile = fixture.FindTile("Alpha-01.png");
        var longTile = fixture.FindTile(longName);
        Assert.NotNull(fixture.FindTile("Bravo-02.png"));
        Assert.NotNull(fixture.FindTile("Nested"));
        Assert.NotNull(fixture.FindText("資料夾 (1)"));
        Assert.NotNull(fixture.FindText("圖片 (3)"));
        var alphaLabel = alphaTile.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Alpha-01.png");
        Assert.Equal(TextWrapping.Wrap, alphaLabel.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, alphaLabel.TextTrimming);
        Assert.Equal(2, alphaLabel.MaxLines);
        Assert.Null(ToolTip.GetTip(alphaLabel));
        var longTooltip = Assert.IsType<TextBlock>(ToolTip.GetTip(longTile));
        Assert.Equal(longName, longTooltip.Text);
        Assert.Equal(TextWrapping.Wrap, longTooltip.TextWrapping);
        Assert.Equal(TextTrimming.None, longTooltip.TextTrimming);
        var labelTopLeft = alphaLabel.TranslatePoint(new Point(), alphaTile);
        Assert.NotNull(labelTopLeft);
        Assert.True(
            labelTopLeft.Value.Y + alphaLabel.Bounds.Height <= alphaTile.Bounds.Height,
            "Tile layout clipped the file name row.");
    }

    [AvaloniaFact]
    public async Task Refresh_library_reloads_visible_thumbnails()
    {
        using var fixture = PicLensHeadlessFixture.StartThumbnailableSeeded(nameof(Refresh_library_reloads_visible_thumbnails));
        await fixture.WaitForLibraryCountAsync(3);
        await fixture.WaitForTileThumbnailAsync("Alpha-01.bmp");

        fixture.ExecuteButtonCommand("TitleBarRefreshLibraryButton");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.StatusMessage.Contains("已重新整理", StringComparison.Ordinal),
            "refresh did not complete");

        await fixture.WaitForTileThumbnailAsync("Alpha-01.bmp");
    }

    [AvaloniaFact]
    public async Task Sort_and_recursive_toggle_update_visible_gallery_and_settings()
    {
        using var fixture = PicLensHeadlessFixture.StartSeeded(nameof(Sort_and_recursive_toggle_update_visible_gallery_and_settings));
        await fixture.WaitForLibraryCountAsync(3);

        fixture.ExecuteMenuItem("TitleBarSortMenuButton", "SortByNameDescendingMenuItem");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.StatusMessage.Contains("名稱由大到小", StringComparison.Ordinal),
            "sort status did not update");

        fixture.ExecuteButtonCommand("TitleBarRecursiveModeToggle");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.LibraryItems.Any(item => item.Name == "Nested-03.png"),
            "recursive item did not load");
        await fixture.WaitForSettingsAsync(settings =>
            settings.IncludeSubfolders
            && settings.Sort.Key == SortKey.Name
            && settings.Sort.Direction == SortDirection.Desc);
    }

    [AvaloniaFact]
    public async Task Folder_history_buttons_navigate_back_and_forward()
    {
        using var fixture = PicLensHeadlessFixture.StartSeeded(nameof(Folder_history_buttons_navigate_back_and_forward));
        await fixture.WaitForLibraryCountAsync(3);

        fixture.ClickTile("Nested");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.LibraryItems.Any(item => item.Name == "Nested-03.png"),
            "nested folder did not open");
        await fixture.WaitForSettingsAsync(settings => settings.LastFolderPath == fixture.LibraryRoot);

        fixture.ExecuteButtonCommand("TitleBarBackButton");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.LibraryItems.Any(item => item.Name == "Alpha-01.png"),
            "back navigation did not return to root");

        fixture.ExecuteButtonCommand("TitleBarForwardButton");
        await fixture.WaitForConditionAsync(
            () => fixture.View.ViewModel.LibraryItems.Any(item => item.Name == "Nested-03.png"),
            "forward navigation did not return to nested folder");
    }

    [AvaloniaFact]
    public async Task Tile_selection_supports_ctrl_multi_select_and_enter_opens_viewer()
    {
        using var fixture = PicLensHeadlessFixture.StartSeeded(nameof(Tile_selection_supports_ctrl_multi_select_and_enter_opens_viewer));
        await fixture.WaitForLibraryCountAsync(3);

        fixture.ClickTile("Alpha-01.png");
        Assert.Equal(1, fixture.View.ViewModel.SelectedImageCount);
        Assert.True(fixture.View.ViewModel.HasSingleSelectedImage);

        fixture.ClickTile("Bravo-02.png", RawInputModifiers.Control);
        Assert.Equal(2, fixture.View.ViewModel.SelectedImageCount);
        Assert.False(fixture.View.ViewModel.HasSingleSelectedImage);

        fixture.ClickTile("Alpha-01.png");
        fixture.PressEnter();
        await fixture.WaitForConditionAsync(
            () => fixture.Window.Title == "PicLens - Alpha-01.png",
            "viewer title did not update");
        Assert.True(fixture.FindByAutomationId<Control>("ViewerSurface").IsVisible);
        Assert.NotNull(fixture.FindByAutomationId<Button>("ViewerPreviousButton"));
        Assert.NotNull(fixture.FindByAutomationId<Button>("ViewerNextButton"));
        Assert.NotNull(fixture.FindByAutomationId<Button>("ViewerZoomInButton"));
        Assert.NotNull(fixture.FindByAutomationId<Button>("ViewerZoomOutButton"));
        Assert.Equal(Color.Parse("#33FFFFFF"), fixture.HoverButtonBackground("ViewerZoomInButton"));
        Assert.Equal(Color.Parse("#4DFFFFFF"), fixture.PressedButtonBackground("ViewerZoomInButton"));
    }

    [AvaloniaFact]
    public async Task Minimum_window_width_keeps_primary_commands_reachable()
    {
        using var fixture = PicLensHeadlessFixture.StartEmpty(nameof(Minimum_window_width_keeps_primary_commands_reachable));
        await fixture.WaitForConditionAsync(() => fixture.View.ViewModel.HasNoCurrentFolder, "empty state did not load");

        fixture.Resize(760, 520);

        foreach (var automationId in new[] { "TitleBarSidebarToggleButton", "LibrarySearchBox", "TitleBarOpenFolderButton", "TitleBarMoreActionsButton" })
        {
            var control = fixture.FindByAutomationId<Control>(automationId);
            var position = control.TranslatePoint(new Point(), fixture.Window);
            Assert.NotNull(position);
            Assert.InRange(position.Value.X, 0, fixture.Window.ClientSize.Width - control.Bounds.Width);
        }
    }

    [AvaloniaFact]
    public async Task Library_items_support_keyboard_focus_selection_and_open()
    {
        using var fixture = PicLensHeadlessFixture.StartSeeded(nameof(Library_items_support_keyboard_focus_selection_and_open));
        await fixture.WaitForLibraryCountAsync(3);
        var tile = fixture.FindTile("Alpha-01.png");

        Assert.True(tile.Focus());
        fixture.PressKey(Key.Space);
        Assert.True(tile.IsFocused);
        Assert.Equal(1, fixture.View.ViewModel.SelectedImageCount);

        fixture.PressKey(Key.Enter);
        await fixture.WaitForConditionAsync(
            () => fixture.Window.Title == "PicLens - Alpha-01.png",
            "keyboard activation did not open the viewer");
    }
}

internal sealed class PicLensHeadlessFixture : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(25);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    private static readonly byte[] BmpBytes = Convert.FromBase64String(
        "Qk1mAAAAAAAAADYAAAAoAAAABAAAAAQAAAABABgAAAAAADAAAAAAAAAAAAAAAAAAAAAAAAAAAACAVQCAqgCA/wCAAFWAVVWAqlWA/1WAAKqAVaqAqqqA/6qAAP+AVf+Aqv+A//+A");

    private readonly string? previousDataRoot;

    private PicLensHeadlessFixture(string testName, bool seedLibrary, bool useThumbnailableImages = false)
    {
        Root = Path.Combine(Path.GetTempPath(), "PicLens.Ui.Tests", testName, Guid.NewGuid().ToString("N"));
        DataRoot = Path.Combine(Root, "data");
        LibraryRoot = Path.Combine(Root, "library");
        Directory.CreateDirectory(DataRoot);

        if (seedLibrary)
        {
            SeedLibrary(useThumbnailableImages);
        }

        previousDataRoot = Environment.GetEnvironmentVariable(AppDataPaths.DataRootEnvironmentVariable);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootEnvironmentVariable, DataRoot);

        Window = new MainWindow
        {
            Width = 1220,
            Height = 820
        };
        Window.Show();
        FlushUi();
        View = Window.GetVisualDescendants().OfType<PicLens.Views.MainView>().Single();
    }

    public static PicLensHeadlessFixture StartEmpty(string testName) => new(testName, seedLibrary: false);

    public static PicLensHeadlessFixture StartSeeded(string testName) => new(testName, seedLibrary: true);

    public static PicLensHeadlessFixture StartThumbnailableSeeded(string testName) =>
        new(testName, seedLibrary: true, useThumbnailableImages: true);

    public string Root { get; }

    public string DataRoot { get; }

    public string LibraryRoot { get; }

    public MainWindow Window { get; }

    public PicLens.Views.MainView View { get; }

    public async Task WaitForLibraryCountAsync(int count) =>
        await WaitForConditionAsync(
            () => View.ViewModel.LibraryItems.Count == count,
            $"library count did not become {count}");

    public async Task WaitForSettingsAsync(Func<AppSettings, bool> predicate) =>
        await WaitForConditionAsync(
            () =>
            {
                var path = Path.Combine(DataRoot, "piclens-settings.json");
                if (!File.Exists(path))
                {
                    return false;
                }

                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
                return settings is not null && predicate(settings);
            },
            "settings predicate was not satisfied");

    public async Task WaitForTileThumbnailAsync(string name) =>
        await WaitForConditionAsync(
            () =>
            {
                var tile = Window.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(control =>
                        control.Classes.Contains("tile")
                        && control.DataContext is LibraryTileItem item
                        && item.Name == name);
                return tile?.DataContext is LibraryTileItem
                {
                    CanShowThumbnail: true,
                    ThumbnailPath: { Length: > 0 } thumbnailPath
                } && File.Exists(thumbnailPath);
            },
            $"thumbnail did not load for {name}");

    public async Task WaitForConditionAsync(Func<bool> predicate, string timeoutMessage)
    {
        var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            FlushUi();
            if (predicate())
            {
                return;
            }

            await Task.Delay(RetryInterval);
        }

        throw new InvalidOperationException(timeoutMessage);
    }

    public T FindByAutomationId<T>(string automationId)
        where T : Control
    {
        FlushUi();
        return Descendants<T>()
            .FirstOrDefault(control => AutomationProperties.GetAutomationId(control) == automationId)
            ?? throw new InvalidOperationException($"AutomationId was not found: {automationId}");
    }

    public IReadOnlyList<string?> MenuHeaders(string buttonAutomationId)
    {
        var button = FindByAutomationId<Button>(buttonAutomationId);
        var flyout = Assert.IsType<MenuFlyout>(button.Flyout);
        return flyout.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).ToList();
    }

    public void ExecuteMenuItem(string buttonAutomationId, string menuItemAutomationId)
    {
        var button = FindByAutomationId<Button>(buttonAutomationId);
        var flyout = Assert.IsType<MenuFlyout>(button.Flyout);
        var item = flyout.Items.OfType<MenuItem>()
            .Single(item => AutomationProperties.GetAutomationId(item) == menuItemAutomationId);
        if (item.Command is not null)
        {
            item.Command.Execute(item.CommandParameter);
        }
        else
        {
            View.ViewModel.ChangeSortOptionCommand.Execute(item.CommandParameter);
        }

        FlushUi();
    }

    public void ExecuteButtonCommand(string automationId)
    {
        var button = FindByAutomationId<Button>(automationId);
        if (button.Command is not null)
        {
            button.Command.Execute(button.CommandParameter);
        }
        else
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        }

        FlushUi();
    }

    public Control FindTile(string name)
    {
        FlushUi();
        return Window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control =>
                control.Classes.Contains("tile")
                && control.DataContext is LibraryTileItem item
                && item.Name == name)
            ?? throw new InvalidOperationException($"Tile was not found: {name}");
    }

    public TextBlock FindText(string text)
    {
        FlushUi();
        return Window.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(textBlock => textBlock.Text == text)
            ?? throw new InvalidOperationException($"Text was not found: {text}");
    }

    public void ClickTile(string name, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var tile = FindTile(name);
        Click(tile, MouseButton.Left, modifiers);
    }

    public void PressEnter()
    {
        Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        FlushUi();
    }

    public void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var physicalKey = key switch
        {
            Key.Space => PhysicalKey.Space,
            Key.Enter => PhysicalKey.Enter,
            _ => PhysicalKey.None
        };
        Window.KeyPress(key, modifiers, physicalKey, null);
        Window.KeyRelease(key, modifiers, physicalKey, null);
        FlushUi();
    }

    public void Resize(double width, double height)
    {
        Window.Width = width;
        Window.Height = height;
        FlushUi();
    }

    public Color HoverButtonBackground(string automationId)
    {
        var button = FindByAutomationId<Button>(automationId);
        Window.MouseMove(CenterOf(button), RawInputModifiers.None);
        FlushUi();
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
        return Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
    }

    public Color PressedButtonBackground(string automationId)
    {
        var button = FindByAutomationId<Button>(automationId);
        var point = CenterOf(button);
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        FlushUi();
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
        var color = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
        var releasePoint = new Point(0, Window.ClientSize.Height - 1);
        Window.MouseMove(releasePoint, RawInputModifiers.None);
        Window.MouseUp(releasePoint, MouseButton.Left, RawInputModifiers.None);
        FlushUi();
        return color;
    }

    public void Dispose()
    {
        Window.Close();
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootEnvironmentVariable, previousDataRoot);
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // Test cleanup is best-effort; temp files are isolated by GUID.
        }
    }

    private void Click(Control control, MouseButton button, RawInputModifiers modifiers)
    {
        var point = CenterOf(control);
        Window.MouseDown(point, button, modifiers);
        Window.MouseUp(point, button, modifiers);
        FlushUi();
    }

    private Point CenterOf(Control control)
    {
        FlushUi();
        return control.TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                Window)
            ?? throw new InvalidOperationException("Could not translate control point to window.");
    }

    private IEnumerable<T> Descendants<T>()
        where T : Control =>
        new Control[] { Window }
            .Concat(Window.GetVisualDescendants().OfType<Control>())
            .Concat(Window.GetLogicalDescendants().OfType<Control>())
            .OfType<T>();

    private void SeedLibrary(bool useThumbnailableImages)
    {
        Directory.CreateDirectory(LibraryRoot);
        var nested = Path.Combine(LibraryRoot, "Nested");
        Directory.CreateDirectory(nested);

        if (useThumbnailableImages)
        {
            WriteBmp(Path.Combine(LibraryRoot, "Alpha-01.bmp"));
            WriteBmp(Path.Combine(LibraryRoot, "Bravo-02.bmp"));
            WriteBmp(Path.Combine(nested, "Nested-03.bmp"));
        }
        else
        {
            WritePng(Path.Combine(LibraryRoot, "Alpha-01.png"));
            WritePng(Path.Combine(LibraryRoot, "Bravo-02.png"));
            WritePng(Path.Combine(nested, "Nested-03.png"));
        }

        var settings = new
        {
            lastFolderPath = LibraryRoot,
            sort = new { key = 0, direction = 0 },
            includeSubfolders = false,
            thumbnailSize = 160
        };
        File.WriteAllText(
            Path.Combine(DataRoot, "piclens-settings.json"),
            JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void WritePng(string path) => File.WriteAllBytes(path, TinyPngBytes);

    private static void WriteBmp(string path) => File.WriteAllBytes(path, BmpBytes);

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

}
