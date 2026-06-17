using System.Text;
using System.Threading.Channels;
using PicLens.Infrastructure.Services;

namespace PicLens.Diagnostics;

public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private const int MaxQueuedLogMessages = 5000;
    private static readonly TimeSpan DisposeFlushTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<DateTimeOffset> now;
    private readonly Channel<string> logChannel;
    private readonly CancellationTokenSource cts = new();
    private readonly Task writeTask;

    public FileAppLogger(string logPath, Func<DateTimeOffset>? now = null)
    {
        LogPath = logPath;
        this.now = now ?? (() => DateTimeOffset.Now);

        // ponytail: bounded queue; add priority/drop counters only if lost old logs become a real debugging problem.
        logChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxQueuedLogMessages)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        writeTask = Task.Run(ProcessLogQueueAsync);
    }

    public string LogPath { get; }

    public static string DefaultLogPath()
        => AppDataPaths.LogPath();

    public void Info(string message) => Write("INFO", message, exception: null);

    public void Error(Exception exception, string message) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder()
                .Append(now().ToString("O"))
                .Append(' ')
                .Append('[')
                .Append(level)
                .Append("] ")
                .AppendLine(message);

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            logChannel.Writer.TryWrite(builder.ToString());
        }
        catch
        {
            // Logging must never become the reason the app exits.
        }
    }

    private async Task ProcessLogQueueAsync()
    {
        var directory = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
                // Ignore
            }
        }

        var reader = logChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(cts.Token))
            {
                while (reader.TryRead(out var logMessage))
                {
                    try
                    {
                        await File.AppendAllTextAsync(LogPath, logMessage, cts.Token);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public void Dispose()
    {
        logChannel.Writer.Complete();
        try
        {
            if (!writeTask.Wait(DisposeFlushTimeout))
            {
                cts.Cancel();
                writeTask.Wait(DisposeFlushTimeout);
            }
        }
        catch
        {
            // Ignore
        }
        cts.Dispose();
    }
}
