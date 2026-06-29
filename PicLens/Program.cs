using Avalonia;
using System;

namespace PicLens;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        ConfigureDeveloperTools(AppBuilder.Configure<App>()
            .UsePlatformDetect())
            .LogToTrace();

    private static AppBuilder ConfigureDeveloperTools(AppBuilder builder)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("PICLENS_ENABLE_DEVTOOLS") == "1")
        {
            return builder.WithDeveloperTools();
        }
#endif
        return builder;
    }
}
