using ImageViewerWin.Diagnostics;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class AppLoggingTests
{
    [Fact]
    public void FileAppLogger_writes_exception_context_and_details()
    {
        using var workspace = new TempDirectory();
        var logPath = Path.Combine(workspace.Path, "ImageViewerWin.log");
        var logger = new FileAppLogger(
            logPath,
            () => new DateTimeOffset(2026, 6, 6, 12, 34, 56, TimeSpan.FromHours(8)));

        logger.Error(new InvalidOperationException("boom"), "IncludeSubfoldersChanged");

        var log = File.ReadAllText(logPath);
        Assert.Contains("2026-06-06T12:34:56.0000000+08:00", log);
        Assert.Contains("[ERROR]", log);
        Assert.Contains("IncludeSubfoldersChanged", log);
        Assert.Contains("System.InvalidOperationException", log);
        Assert.Contains("boom", log);
    }

    [Fact]
    public void App_registers_global_exception_logging_hooks()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "App.xaml.cs"));

        Assert.Contains("UnhandledException += OnUnhandledException", code);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException", code);
        Assert.Contains("TaskScheduler.UnobservedTaskException += OnUnobservedTaskException", code);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImageViewerWin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
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
