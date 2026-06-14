using System.Diagnostics;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using FlaUIApplication = FlaUI.Core.Application;

namespace PicLens.Ui.Tests;

public sealed class MainWindowSmokeTests : IClassFixture<PicLensAppFixture>
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

    private readonly PicLensAppFixture fixture;

    public MainWindowSmokeTests(PicLensAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Main_window_exposes_primary_controls_empty_state_and_menus()
    {
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
}

public sealed class PicLensAppFixture : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(150);

    private readonly UIA3Automation automation;
    private readonly FlaUIApplication app;
    private readonly Window mainWindow;

    public PicLensAppFixture()
    {
        AppPath = ResolveAppPath();
        ArtifactsRoot = Path.Combine(RepositoryRoot(), "artifacts", "ui-tests", DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff"));
        DataRoot = Path.Combine(ArtifactsRoot, "data");
        Directory.CreateDirectory(DataRoot);

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
            DismissStartupFolderPickerIfPresent();
            FindByAutomationId("AppTitleBar");
        }
        catch (Exception exception)
        {
            CaptureDiagnostics("startup", exception);
            TryCloseApplication();
            throw;
        }
    }

    public string AppPath { get; }

    public string ArtifactsRoot { get; }

    public string DataRoot { get; }

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

    public AutomationElement FindByAutomationId(string automationId)
    {
        var result = Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(condition => condition.ByAutomationId(automationId)),
            DefaultTimeout,
            RetryInterval);

        return result.Result
            ?? throw new InvalidOperationException($"AutomationId was not found: {automationId}");
    }

    public AutomationElement FindByName(string name)
    {
        var result = Retry.WhileNull(
            () => automation.GetDesktop().FindFirstDescendant(condition => condition.ByName(name)),
            DefaultTimeout,
            RetryInterval,
            ignoreException: true);

        return result.Result
            ?? throw new InvalidOperationException($"UI element name was not found: {name}");
    }

    public void OpenMenuAndAssertItems(string menuButtonAutomationId, params string[] itemNames)
    {
        var menuButton = FindByAutomationId(menuButtonAutomationId);
        if (menuButton.Patterns.Invoke.IsSupported)
        {
            menuButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            menuButton.Click();
        }

        foreach (var itemName in itemNames)
        {
            Assert.NotNull(FindByName(itemName));
        }

        Keyboard.Press(VirtualKeyShort.ESCAPE);
    }

    private void DismissStartupFolderPickerIfPresent()
    {
        var cancelButton = Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(condition => condition.ByName("取消")),
            TimeSpan.FromSeconds(2),
            RetryInterval);

        if (cancelButton.Result is null)
        {
            return;
        }

        cancelButton.Result.Click();
        _ = Retry.WhileNull(
            () => mainWindow.FindFirstDescendant(condition => condition.ByAutomationId("EmptyStateOpenFolderButton")),
            DefaultTimeout,
            RetryInterval,
            throwOnTimeout: true,
            timeoutMessage: "Empty state did not appear after dismissing the startup folder picker.");
    }

    public void CaptureDiagnostics(string testName, Exception exception)
    {
        var artifactDirectory = Path.Combine(ArtifactsRoot, SanitizeFileName(testName));
        Directory.CreateDirectory(artifactDirectory);

        var failureDetails = new StringBuilder()
            .AppendLine($"AppPath: {AppPath}")
            .AppendLine($"ProcessId: {(app is null ? "<not started>" : ProcessId.ToString())}")
            .AppendLine($"DataRoot: {DataRoot}")
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
    }

    public void Dispose()
        => TryCloseApplication();

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

    private string LogPath => Path.Combine(DataRoot, "Logs", "PicLens.log");

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
