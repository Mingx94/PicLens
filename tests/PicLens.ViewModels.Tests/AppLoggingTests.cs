using PicLens.Core.Models;
using PicLens.Core.Domain;
using PicLens.Diagnostics;
using PicLens.Application.Services;

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
    public void FileAppLogger_disposes_after_many_info_messages()
    {
        using var workspace = new TempDirectory();
        var logPath = Path.Combine(workspace.Path, "PicLens.log");

        using (var logger = new FileAppLogger(logPath))
        {
            for (var index = 0; index < 6000; index += 1)
            {
                logger.Info($"message-{index}");
            }
        }

        Assert.True(File.Exists(logPath));
        Assert.NotEmpty(File.ReadAllText(logPath));
    }

    [Fact]
    public void MainPageViewModel_path_display_properties_log_invalid_paths()
    {
        var logger = new RecordingLogger();
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService(),
            _ => { },
            appLogger: logger);
        viewModel.CurrentFolderPath = "bad\0path";

        Assert.Equal("資料夾", viewModel.CurrentParentFolderName);
        Assert.Equal("bad\0path", viewModel.CurrentFolderName);

        Assert.Contains(logger.Errors, error => error.Message.StartsWith("Parent folder name lookup failed.", StringComparison.Ordinal));
        Assert.Contains(logger.Errors, error => error.Message.StartsWith("Folder segment lookup failed.", StringComparison.Ordinal));
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
