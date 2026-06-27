using PicLens.Diagnostics;
using PicLens.Infrastructure.Services;

namespace PicLens.Infrastructure.Tests;

public sealed class FileAppLoggerTests
{
    [Fact]
    public void Default_path_uses_piclens_log_path()
    {
        var logPath = FileAppLogger.DefaultLogPath();

        Assert.EndsWith(
            Path.Combine("PicLens", "Logs", "PicLens.log"),
            logPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_path_uses_piclens_data_root_when_set()
    {
        using var environment = EnvironmentVariableScope.Set(
            AppDataPaths.DataRootEnvironmentVariable,
            Path.Combine(Path.GetTempPath(), "PicLens.Infrastructure.Tests", Guid.NewGuid().ToString("N")));

        var logPath = FileAppLogger.DefaultLogPath();

        Assert.Equal(Path.Combine(Path.GetFullPath(environment.Value!), "Logs", "PicLens.log"), logPath);
    }

    [Fact]
    public void Writes_exception_context_and_details()
    {
        using var workspace = TempWorkspace.Create();
        var logPath = Path.Combine(workspace.Root, "PicLens.log");
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
    public void Disposes_after_many_info_messages()
    {
        using var workspace = TempWorkspace.Create();
        var logPath = Path.Combine(workspace.Root, "PicLens.log");

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
}
