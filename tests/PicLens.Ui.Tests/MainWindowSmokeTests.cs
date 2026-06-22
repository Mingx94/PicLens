using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using FlaUIApplication = FlaUI.Core.Application;

namespace PicLens.Ui.Tests;

public sealed class MainWindowSmokeTests
{
    private static readonly string[] MainWindowAutomationIds =
    [
        "AppTitleBar",
        "FolderNavigationCommandBar",
        "LibraryCommandBar",
        "TitleBarOpenFolderButton",
        "TitleBarSortMenuButton",
        "TitleBarRecursiveModeToggle",
        "TitleBarMoreActionsButton",
        "FolderTree",
        "LibraryGrid",
        "StatusInfoBar",
        "ThumbnailSizeSlider",
        "EmptyStateOpenFolderButton"
    ];

    [Fact]
    public void Main_window_exposes_primary_controls_empty_state_and_menus()
    {
        using var fixture = PicLensAppFixture.StartEmpty(nameof(Main_window_exposes_primary_controls_empty_state_and_menus));

        fixture.WithDiagnostics(nameof(Main_window_exposes_primary_controls_empty_state_and_menus), () =>
        {
            Assert.Equal("PicLens", fixture.MainWindow.Title);

            foreach (var automationId in MainWindowAutomationIds)
            {
                Assert.NotNull(fixture.FindByAutomationId(automationId));
            }

            fixture.OpenMenuAndAssertItems(
                "TitleBarSortMenuButton",
                "名稱由小到大",
                "名稱由大到小",
                "修改時間最舊到最新",
                "修改時間最新到最舊");

            fixture.OpenMenuAndAssertItems(
                "TitleBarMoreActionsButton",
                "將目前顯示項目轉為 JPG",
                "清除同名非 JPG 檔案");
        });
    }

    [Fact]
    public void Seeded_library_loads_folder_tree_grid_status_and_thumbnails()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Seeded_library_loads_folder_tree_grid_status_and_thumbnails));

        fixture.WithDiagnostics(nameof(Seeded_library_loads_folder_tree_grid_status_and_thumbnails), () =>
        {
            Assert.NotNull(fixture.FindByAutomationId("FolderTree"));
            Assert.NotNull(fixture.FindByAutomationId("LibraryGrid"));
            Assert.NotNull(fixture.FindByTilePrefix("Alpha-01.png，圖片"));
            Assert.NotNull(fixture.FindByTilePrefix("Bravo-02.png，圖片"));
            Assert.NotNull(fixture.FindByTilePrefix("Nested，資料夾"));
            fixture.WaitForVisibleText("已從", "載入 3 個項目");
        });
    }

    [Fact]
    public void Sort_and_recursive_toggle_update_visible_gallery_and_settings()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Sort_and_recursive_toggle_update_visible_gallery_and_settings));

        fixture.WithDiagnostics(nameof(Sort_and_recursive_toggle_update_visible_gallery_and_settings), () =>
        {
            fixture.InvokeMenuItem("TitleBarSortMenuButton", "名稱由大到小");
            fixture.WaitForVisibleText("排序已變更為 名稱由大到小");

            fixture.ClickByAutomationId("TitleBarRecursiveModeToggle");
            Assert.NotNull(fixture.FindByTilePrefix("Nested-03.png，圖片"));
            fixture.WaitForSettings(settings =>
                settings.IncludeSubfolders
                && settings.Sort.Key == 0
                && settings.Sort.Direction == 1);
        });
    }

    [Fact]
    public void Folder_history_buttons_navigate_back_and_forward()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Folder_history_buttons_navigate_back_and_forward));

        fixture.WithDiagnostics(nameof(Folder_history_buttons_navigate_back_and_forward), () =>
        {
            fixture.ClickTile("Nested，資料夾");
            Assert.NotNull(fixture.FindByTilePrefix("Nested-03.png，圖片"));

            fixture.ClickByAutomationId("TitleBarBackButton");
            Assert.NotNull(fixture.FindByTilePrefix("Alpha-01.png，圖片"));

            fixture.ClickByAutomationId("TitleBarForwardButton");
            Assert.NotNull(fixture.FindByTilePrefix("Nested-03.png，圖片"));
        });
    }

    [Fact]
    public void Left_click_selects_image_without_action_bar_and_right_click_opens_context_menu()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Left_click_selects_image_without_action_bar_and_right_click_opens_context_menu));

        fixture.WithDiagnostics(nameof(Left_click_selects_image_without_action_bar_and_right_click_opens_context_menu), () =>
        {
            fixture.ClickTile("Alpha-01.png，圖片");

            fixture.WaitForAutomationIdGone("SelectionSummaryText");
            fixture.RightClickTile("Alpha-01.png，圖片");
            Assert.NotNull(fixture.FindByAutomationId("ImageContextRevealInFileExplorerButton"));
            Assert.NotNull(fixture.FindByAutomationId("ImageContextRenameButton"));
            Assert.NotNull(fixture.FindByAutomationId("ImageContextTrashButton"));
        });
    }

    [Fact]
    public void Rename_dialog_cancel_does_not_change_file()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Rename_dialog_cancel_does_not_change_file));

        fixture.WithDiagnostics(nameof(Rename_dialog_cancel_does_not_change_file), () =>
        {
            var originalPath = Path.Combine(fixture.LibraryRoot, "Alpha-01.png");
            var canceledTargetPath = Path.Combine(fixture.LibraryRoot, "Alpha-01-renamed.png");

            fixture.RightClickTile("Alpha-01.png，圖片");
            fixture.ClickByAutomationId("ImageContextRenameButton");
            fixture.ClickByName("取消");

            Assert.True(File.Exists(originalPath));
            Assert.False(File.Exists(canceledTargetPath));
            Assert.NotNull(fixture.FindByTilePrefix("Alpha-01.png，圖片"));
        });
    }

    [Fact]
    public void Thumbnail_size_slider_persists_setting()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Thumbnail_size_slider_persists_setting));

        fixture.WithDiagnostics(nameof(Thumbnail_size_slider_persists_setting), () =>
        {
            fixture.SetSliderValue("ThumbnailSizeSlider", 200);
            fixture.WaitForVisibleText("縮圖大小已調整為 200");
            fixture.WaitForSettings(settings => settings.ThumbnailSize == 200);
        });
    }

    [Fact]
    public void Double_clicking_image_opens_inline_viewer_and_viewer_controls_work()
    {
        using var fixture = PicLensAppFixture.StartSeeded(nameof(Double_clicking_image_opens_inline_viewer_and_viewer_controls_work));

        fixture.WithDiagnostics(nameof(Double_clicking_image_opens_inline_viewer_and_viewer_controls_work), () =>
        {
            fixture.DoubleClickTile("Alpha-01.png，圖片");

            Assert.NotNull(fixture.FindByAutomationId("ViewerSurface"));
            fixture.WaitForWindowTitle("PicLens - Alpha-01.png");
            Assert.NotNull(fixture.FindByAutomationId("ViewerPreviousButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerNextButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerZoomOutButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerResetZoomButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerZoomInButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerCloseButton"));
            Assert.NotNull(fixture.FindByAutomationId("ViewerImage"));

            fixture.ClickByAutomationId("ViewerNextButton");
            fixture.WaitForWindowTitle("PicLens - Bravo-02.png");

            fixture.ClickByAutomationId("ViewerZoomInButton");

            Keyboard.Press(VirtualKeyShort.ESCAPE);
            fixture.WaitForInlineViewerClosed();
            fixture.WaitForWindowTitle("PicLens");
        });
    }
}

public sealed class PicLensAppFixture : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(150);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    private readonly UIA3Automation automation;
    private readonly FlaUIApplication app;
    private readonly Window mainWindow;

    private PicLensAppFixture(string testName, bool seedLibrary)
    {
        AppPath = ResolveAppPath();
        var artifactName = $"{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{SanitizeFileName(testName)}";
        ArtifactsRoot = Path.Combine(RepositoryRoot(), "artifacts", "ui-tests", artifactName);
        DataRoot = Path.Combine(ArtifactsRoot, "data");
        LibraryRoot = Path.Combine(ArtifactsRoot, "library");
        Directory.CreateDirectory(DataRoot);

        if (seedLibrary)
        {
            SeedLibrary();
        }

        try
        {
            var startInfo = new ProcessStartInfo(AppPath)
            {
                WorkingDirectory = Path.GetDirectoryName(AppPath) ?? ".",
                UseShellExecute = false
            };
            startInfo.Environment["PICLENS_DATA_ROOT"] = DataRoot;

            app = FlaUIApplication.Launch(startInfo);
            automation = new UIA3Automation();
            mainWindow = app.GetMainWindow(automation, DefaultTimeout)
                ?? throw new InvalidOperationException("PicLens main window was not found.");
            mainWindow.Focus();

            if (!seedLibrary)
            {
                DismissStartupFolderPickerIfPresent();
            }

            FindByAutomationId("AppTitleBar");
            if (seedLibrary)
            {
                FindByTilePrefix("Alpha-01.png，圖片");
            }
        }
        catch (Exception exception)
        {
            CaptureDiagnostics("startup", exception);
            TryCloseApplication();
            throw;
        }
    }

    public static PicLensAppFixture StartEmpty(string testName) =>
        new(testName, seedLibrary: false);

    public static PicLensAppFixture StartSeeded(string testName) =>
        new(testName, seedLibrary: true);

    public string AppPath { get; }

    public string ArtifactsRoot { get; }

    public string DataRoot { get; }

    public string LibraryRoot { get; }

    public int ProcessId => app.ProcessId;

    public Window MainWindow => mainWindow;

    public void WithDiagnostics(string testName, Action test)
    {
        try
        {
            test();
        }
        catch (Exception exception)
        {
            CaptureDiagnostics(testName, exception);
            throw;
        }
    }

    public AutomationElement FindByAutomationId(string automationId) =>
        FindByAutomationId(mainWindow, automationId);

    public AutomationElement FindByAutomationId(AutomationElement root, string automationId)
    {
        return WaitForElement(
            () => root.FindFirstDescendant(condition => condition.ByAutomationId(automationId)),
            $"AutomationId was not found: {automationId}");
    }

    public AutomationElement FindByTilePrefix(string namePrefix) =>
        WaitForElement(
            () =>
            {
                var libraryGrid = mainWindow.FindFirstDescendant(condition => condition.ByAutomationId("LibraryGrid"));
                var tileAutomationId = TileAutomationIdFromPrefix(namePrefix);
                var byAutomationId = libraryGrid?.FindFirstDescendant(condition => condition.ByAutomationId(tileAutomationId))
                    ?? libraryGrid?.FindFirstDescendant(condition => condition.ByAutomationId($"{tileAutomationId}_Container"));
                if (byAutomationId is not null)
                {
                    return byAutomationId;
                }

                return libraryGrid?.FindAllDescendants()
                    .FirstOrDefault(element => MatchesTileName(element, namePrefix));
            },
            $"Tile was not found: {namePrefix}");

    public void ClickByAutomationId(string automationId) =>
        ClickByAutomationId(mainWindow, automationId);

    public void ClickByAutomationId(AutomationElement root, string automationId) =>
        InvokeOrClick(FindByAutomationId(root, automationId));

    public void ClickTile(string namePrefix) =>
        FindByTilePrefix(namePrefix).Click();

    public void DoubleClickTile(string namePrefix) =>
        FindByTilePrefix(namePrefix).DoubleClick();

    public void RightClickTile(string namePrefix) =>
        FindByTilePrefix(namePrefix).RightClick();

    public void ClickByName(string name) =>
        InvokeOrClick(FindByName(name));

    public void InvokeMenuItem(string menuButtonAutomationId, string itemName)
    {
        ClickByAutomationId(menuButtonAutomationId);
        InvokeOrClick(FindByName(itemName));
    }

    public void OpenMenuAndAssertItems(string menuButtonAutomationId, params string[] itemNames)
    {
        ClickByAutomationId(menuButtonAutomationId);

        foreach (var itemName in itemNames)
        {
            Assert.NotNull(FindByName(itemName));
        }

        Keyboard.Press(VirtualKeyShort.ESCAPE);
    }

    public void SetSliderValue(string automationId, double value)
    {
        var slider = FindByAutomationId(automationId);
        slider.Focus();
        slider.Patterns.RangeValue.Pattern.SetValue(value);
        FindByAutomationId("LibraryGrid").Focus();
    }

    public void WaitForAutomationIdGone(string automationId)
    {
        WaitForCondition(
            () => mainWindow.FindFirstDescendant(condition => condition.ByAutomationId(automationId)) is null,
            $"AutomationId was still present: {automationId}");
    }

    public void WaitForSettings(Func<UiTestSettings, bool> predicate)
    {
        WaitForCondition(
            () =>
            {
                if (!File.Exists(SettingsPath))
                {
                    return false;
                }

                var settings = JsonSerializer.Deserialize<UiTestSettings>(File.ReadAllText(SettingsPath), JsonOptions);
                return settings is not null && predicate(settings);
            },
            $"Settings predicate was not satisfied. Path={SettingsPath}");
    }

    public void WaitForInlineViewerClosed()
    {
        WaitForCondition(
            () => mainWindow.FindFirstDescendant(condition => condition.ByAutomationId("ViewerSurface")) is null,
            "Inline viewer did not close.");
    }

    public void WaitForWindowTitle(string title)
    {
        WaitForCondition(
            () => string.Equals(mainWindow.Title, title, StringComparison.Ordinal),
            $"Window title did not become: {title}");
    }

    public void WaitForVisibleText(params string[] fragments) =>
        WaitForVisibleText(mainWindow, fragments);

    public void WaitForVisibleText(AutomationElement root, params string[] fragments)
    {
        _ = WaitForElement(
            () => new[] { root }.Concat(root.FindAllDescendants())
                .FirstOrDefault(element => ContainsAllNameFragments(element, fragments)),
            $"Visible text was not found: {string.Join(" | ", fragments)}");
    }

    public void Dispose()
        => TryCloseApplication();

    private string SettingsPath => Path.Combine(DataRoot, "piclens-settings.json");

    private string LogPath => Path.Combine(DataRoot, "Logs", "PicLens.log");

    private void SeedLibrary()
    {
        Directory.CreateDirectory(LibraryRoot);
        var nested = Path.Combine(LibraryRoot, "Nested");
        Directory.CreateDirectory(nested);

        WritePng(Path.Combine(LibraryRoot, "Alpha-01.png"));
        WritePng(Path.Combine(LibraryRoot, "Bravo-02.png"));
        WritePng(Path.Combine(nested, "Nested-03.png"));

        var settings = new
        {
            version = 1,
            lastFolderPath = LibraryRoot,
            sort = new { key = 0, direction = 0 },
            includeSubfolders = false,
            thumbnailSize = 160
        };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void WritePng(string path) =>
        File.WriteAllBytes(path, TinyPngBytes);

    private void DismissStartupFolderPickerIfPresent()
    {
        var cancelButton = WaitForOptional(
            () => mainWindow.FindFirstDescendant(condition => condition.ByName("取消")),
            TimeSpan.FromSeconds(2));

        if (cancelButton is null)
        {
            return;
        }

        cancelButton.Click();
        _ = FindByAutomationId("EmptyStateOpenFolderButton");
    }

    public void CaptureDiagnostics(string testName, Exception exception)
    {
        var artifactDirectory = Path.Combine(ArtifactsRoot, SanitizeFileName(testName));
        Directory.CreateDirectory(artifactDirectory);

        var failureDetails = new StringBuilder()
            .AppendLine($"AppPath: {AppPath}")
            .AppendLine($"ProcessId: {(app is null ? "<not started>" : ProcessId.ToString())}")
            .AppendLine($"DataRoot: {DataRoot}")
            .AppendLine($"LibraryRoot: {LibraryRoot}")
            .AppendLine($"LogPath: {LogPath}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .ToString();
        File.WriteAllText(Path.Combine(artifactDirectory, "failure.txt"), failureDetails);

        if (File.Exists(LogPath))
        {
            File.Copy(LogPath, Path.Combine(artifactDirectory, "PicLens.log"), overwrite: true);
        }

        try
        {
            using var image = app is null
                ? Capture.Screen()
                : Capture.Element(mainWindow);
            image.ToFile(Path.Combine(artifactDirectory, "screenshot.png"));
        }
        catch
        {
            // Screenshot capture is diagnostic best-effort; preserve the original test failure.
        }

        try
        {
            File.WriteAllText(Path.Combine(artifactDirectory, "ui-tree.txt"), DumpUiTree());
        }
        catch
        {
            // UI tree capture is diagnostic best-effort; preserve the original test failure.
        }
    }

    private void TryCloseApplication()
    {
        try
        {
            app?.Close(killIfCloseFails: true);
            automation?.Dispose();
            app?.Dispose();
        }
        catch
        {
            // Cleanup must not hide the original test failure.
        }
    }

    private AutomationElement FindByName(string name)
    {
        return WaitForElement(
            () => automation.GetDesktop().FindFirstDescendant(condition => condition.ByName(name)),
            $"UI element name was not found: {name}");
    }

    private string DumpUiTree()
    {
        var builder = new StringBuilder();
        foreach (var element in new[] { mainWindow }.Concat(mainWindow.FindAllDescendants()))
        {
            builder
                .Append("AutomationId=")
                .Append(SafeAutomationId(element))
                .Append("; Name=")
                .Append(SafeName(element))
                .Append("; HelpText=")
                .Append(SafeHelpText(element))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static bool MatchesTileName(AutomationElement element, string prefix)
    {
        var name = SafeName(element);
        var displayName = prefix.Split('，')[0];
        return name.StartsWith(prefix, StringComparison.Ordinal)
            || name.Equals(displayName, StringComparison.Ordinal);
    }

    private static string TileAutomationIdFromPrefix(string prefix)
    {
        var displayName = prefix.Split('，')[0];
        var kind = prefix.Contains("資料夾", StringComparison.Ordinal) ? "LibraryFolderTile" : "LibraryImageTile";
        return $"{kind}_{SanitizeAutomationIdSegment(displayName)}";
    }

    private static string SanitizeAutomationIdSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static bool ContainsAllNameFragments(AutomationElement element, IReadOnlyCollection<string> fragments)
    {
        var text = $"{SafeName(element)} {SafeHelpText(element)}";
        return fragments.All(fragment => text.Contains(fragment, StringComparison.Ordinal));
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeHelpText(AutomationElement element)
    {
        try
        {
            return element.Properties.HelpText.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeAutomationId(AutomationElement element)
    {
        try
        {
            return element.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void InvokeOrClick(AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            element.Click();
        }
    }

    private static T WaitForElement<T>(Func<T?> find, string timeoutMessage)
        where T : class
    {
        var result = WaitForOptional(find, DefaultTimeout);
        return result ?? throw new InvalidOperationException(timeoutMessage);
    }

    private static T? WaitForOptional<T>(Func<T?> find, TimeSpan timeout)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var result = find();
                if (result is not null)
                {
                    return result;
                }
            }
            catch
            {
                // UIA trees are transient while WinUI opens flyouts or windows. Retry until timeout.
            }

            Thread.Sleep(RetryInterval);
        }

        return null;
    }

    private static void WaitForCondition(Func<bool> predicate, string timeoutMessage)
    {
        var deadline = DateTimeOffset.UtcNow + DefaultTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                if (predicate())
                {
                    return;
                }
            }
            catch
            {
                // Retry transient UIA and file reads until timeout.
            }

            Thread.Sleep(RetryInterval);
        }

        throw new InvalidOperationException(timeoutMessage);
    }

    private static string ResolveAppPath()
    {
        var appPath = Environment.GetEnvironmentVariable("PICLENS_UI_APP_PATH");
        if (string.IsNullOrWhiteSpace(appPath))
        {
            throw new InvalidOperationException("PICLENS_UI_APP_PATH must point to the published PicLens.exe before running UI tests.");
        }

        var resolvedPath = Path.GetFullPath(appPath);
        return File.Exists(resolvedPath)
            ? resolvedPath
            : throw new FileNotFoundException("PICLENS_UI_APP_PATH does not point to an existing file.", resolvedPath);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicLens.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}

public sealed record UiTestSettings(
    int Version,
    string? LastFolderPath,
    UiTestSortState Sort,
    bool IncludeSubfolders,
    int ThumbnailSize);

public sealed record UiTestSortState(int Key, int Direction);
