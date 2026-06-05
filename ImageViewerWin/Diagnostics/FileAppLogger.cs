using System.Text;

namespace ImageViewerWin.Diagnostics;

public sealed class FileAppLogger : IAppLogger
{
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> now;

    public FileAppLogger(string logPath, Func<DateTimeOffset>? now = null)
    {
        LogPath = logPath;
        this.now = now ?? (() => DateTimeOffset.Now);
    }

    public string LogPath { get; }

    public static string DefaultLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "ImageViewerWin", "Logs", "ImageViewerWin.log");
    }

    public void Info(string message) => Write("INFO", message, exception: null);

    public void Error(Exception exception, string message) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

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

            lock (gate)
            {
                File.AppendAllText(LogPath, builder.ToString());
            }
        }
        catch
        {
            // Logging must never become the reason the app exits.
        }
    }
}
