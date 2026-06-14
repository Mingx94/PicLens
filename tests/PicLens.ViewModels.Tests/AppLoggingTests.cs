using PicLens.Diagnostics;

namespace PicLens.ViewModels.Tests;

public sealed class AppLoggingTests
{
    [Fact]
    public void FileAppLogger_default_path_uses_piclens_log_path()
    {
        var logPath = FileAppLogger.DefaultLogPath();

        Assert.EndsWith(
            Path.Combine("PicLens", "Logs", "PicLens.log"),
            logPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileAppLogger_default_path_uses_piclens_data_root_when_set()
    {
        using var environment = EnvironmentVariableScope.Set(
            "PICLENS_DATA_ROOT",
            Path.Combine(Path.GetTempPath(), "PicLens.ViewModels.Tests", Guid.NewGuid().ToString("N")));

        var logPath = FileAppLogger.DefaultLogPath();

        Assert.Equal(Path.Combine(Path.GetFullPath(environment.Value!), "Logs", "PicLens.log"), logPath);
    }

    [Fact]
    public void FileAppLogger_writes_exception_context_and_details()
    {
        using var workspace = new TempDirectory();
        var logPath = Path.Combine(workspace.Path, "PicLens.log");
        using (var logger = new FileAppLogger(
            logPath,
            () => new DateTimeOffset(2026, 6, 6, 12, 34, 56, TimeSpan.FromHours(8))))
        {
            logger.Error(new InvalidOperationException("boom"), "IncludeSubfoldersChanged");
        }

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
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "PicLens", "App.xaml.cs"));

        Assert.Contains("AppDataMigration.MigrateLegacyData(Logger);", code);
        Assert.Contains("UnhandledException += OnUnhandledException", code);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException", code);
        Assert.Contains("TaskScheduler.UnobservedTaskException += OnUnobservedTaskException", code);
    }

    [Fact]
    public void AppDataMigration_copies_legacy_settings_thumbnails_and_log_without_removing_legacy_data()
    {
        using var workspace = new TempDirectory();
        var legacyRoot = Path.Combine(workspace.Path, "ImageViewerWin");
        Directory.CreateDirectory(Path.Combine(legacyRoot, "Thumbnails", "nested"));
        Directory.CreateDirectory(Path.Combine(legacyRoot, "Logs"));
        File.WriteAllText(Path.Combine(legacyRoot, "image-viewer-settings.json"), "legacy-settings");
        File.WriteAllBytes(Path.Combine(legacyRoot, "Thumbnails", "nested", "thumb.png"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(legacyRoot, "Logs", "ImageViewerWin.log"), "legacy-log");
        var logger = new RecordingLogger();

        global::PicLens.AppDataMigration.MigrateLegacyData(logger, workspace.Path);

        var currentRoot = Path.Combine(workspace.Path, "PicLens");
        Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(currentRoot, "piclens-settings.json")));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(currentRoot, "Thumbnails", "nested", "thumb.png")));
        Assert.Equal("legacy-log", File.ReadAllText(Path.Combine(currentRoot, "Logs", "PicLens.log")));
        Assert.True(File.Exists(Path.Combine(legacyRoot, "image-viewer-settings.json")));
        Assert.True(File.Exists(Path.Combine(legacyRoot, "Thumbnails", "nested", "thumb.png")));
        Assert.True(File.Exists(Path.Combine(legacyRoot, "Logs", "ImageViewerWin.log")));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void AppDataMigration_does_not_overwrite_existing_piclens_data()
    {
        using var workspace = new TempDirectory();
        var legacyRoot = Path.Combine(workspace.Path, "ImageViewerWin");
        Directory.CreateDirectory(Path.Combine(legacyRoot, "Thumbnails"));
        Directory.CreateDirectory(Path.Combine(legacyRoot, "Logs"));
        File.WriteAllText(Path.Combine(legacyRoot, "image-viewer-settings.json"), "legacy-settings");
        File.WriteAllText(Path.Combine(legacyRoot, "Thumbnails", "legacy.png"), "legacy-thumbnail");
        File.WriteAllText(Path.Combine(legacyRoot, "Logs", "ImageViewerWin.log"), "legacy-log");

        var currentRoot = Path.Combine(workspace.Path, "PicLens");
        Directory.CreateDirectory(Path.Combine(currentRoot, "Thumbnails"));
        Directory.CreateDirectory(Path.Combine(currentRoot, "Logs"));
        File.WriteAllText(Path.Combine(currentRoot, "piclens-settings.json"), "current-settings");
        File.WriteAllText(Path.Combine(currentRoot, "Thumbnails", "current.png"), "current-thumbnail");
        File.WriteAllText(Path.Combine(currentRoot, "Logs", "PicLens.log"), "current-log");
        var logger = new RecordingLogger();

        global::PicLens.AppDataMigration.MigrateLegacyData(logger, workspace.Path);

        Assert.Equal("current-settings", File.ReadAllText(Path.Combine(currentRoot, "piclens-settings.json")));
        Assert.Equal("current-thumbnail", File.ReadAllText(Path.Combine(currentRoot, "Thumbnails", "current.png")));
        Assert.False(File.Exists(Path.Combine(currentRoot, "Thumbnails", "legacy.png")));
        Assert.Equal("current-log", File.ReadAllText(Path.Combine(currentRoot, "Logs", "PicLens.log")));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void AppDataMigration_logs_expected_migration_failures()
    {
        using var workspace = new TempDirectory();
        var legacyRoot = Path.Combine(workspace.Path, "ImageViewerWin");
        Directory.CreateDirectory(legacyRoot);
        File.WriteAllText(Path.Combine(legacyRoot, "image-viewer-settings.json"), "legacy-settings");
        File.WriteAllText(Path.Combine(workspace.Path, "PicLens"), "not a directory");
        var logger = new RecordingLogger();

        global::PicLens.AppDataMigration.MigrateLegacyData(logger, workspace.Path);

        var error = Assert.Single(logger.Errors);
        Assert.Equal("Legacy app data migration failed.", error.Message);
        Assert.IsType<IOException>(error.Exception);
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

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string name;
        private readonly string? previousValue;

        private EnvironmentVariableScope(string name, string? value)
        {
            this.name = name;
            Value = value;
            previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public string? Value { get; }

        public static EnvironmentVariableScope Set(string name, string? value) => new(name, value);

        public void Dispose() => Environment.SetEnvironmentVariable(name, previousValue);
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public List<(Exception Exception, string Message)> Errors { get; } = [];

        public void Info(string message)
        {
        }

        public void Error(Exception exception, string message) => Errors.Add((exception, message));
    }
}
