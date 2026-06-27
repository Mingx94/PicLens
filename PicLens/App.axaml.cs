using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PicLens.Diagnostics;

namespace PicLens;

public partial class App : Application
{
    private static bool globalExceptionHooksRegistered;

    public static IAppLogger Logger { get; private set; } = NullAppLogger.Instance;

    public static string LogPath { get; private set; } = FileAppLogger.DefaultLogPath();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureLogging();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (_, _) =>
            {
                if (Logger is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureLogging()
    {
        LogPath = FileAppLogger.DefaultLogPath();
        Logger = new FileAppLogger(LogPath);
        Logger.Info($"Application logging initialized. LogPath={LogPath}");

        if (globalExceptionHooksRegistered)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        globalExceptionHooksRegistered = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"Unhandled non-exception object: {e.ExceptionObject}");
        Logger.Error(exception, "Unhandled AppDomain exception.");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error(e.Exception, "Unobserved task exception.");
    }
}
