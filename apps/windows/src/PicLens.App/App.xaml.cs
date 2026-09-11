using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PicLens.Services;
namespace PicLens.App;

public partial class App : Application
{
    public static Profile? Profile { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var options = LaunchOptions.Parse(e.Args); Profile = new(options.DataRoot);
            ApplyTheme(options.Dark); SystemParameters.StaticPropertyChanged += (_, _) => ApplyTheme(options.Dark);
            Profile.Log($"啟動 version={typeof(App).Assembly.GetName().Version?.ToString(3)} profile={BuildProfile}");
            DispatcherUnhandledException += (_, error) => { Profile.Log("UI 未處理錯誤", error.Exception); error.Handled = true; if (MainWindow is MainWindow main) main.Model.Status = error.Exception.Message; };
            var window = new MainWindow(Profile, options); MainWindow = window; window.Show();
        }
        catch (Exception ex) { Profile?.Log("啟動失敗", ex); Console.Error.WriteLine(ex.Message); Shutdown(2); }
    }
    public static string BuildProfile
    {
        get
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
    }
    public void ApplyTheme(bool forceDark)
    {
        bool dark = forceDark || (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0;
        string[] keys = ["Surface", "Card", "Ink", "MutedInk", "Line", "Accent", "Selected"];
        string[] colors = dark ? ["#202726", "#28312F", "#ECF0E9", "#ABB8B2", "#424D47", "#9BD1B8", "#354F43"] :
            ["#F6F5F1", "#FFFFFF", "#243333", "#687471", "#DDDFD8", "#245F51", "#E1EDE7"];
        for (int i = 0; i < keys.Length; i++) Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        Resources["AccentInk"] = dark ? new SolidColorBrush(Color.FromRgb(23, 43, 33)) : Brushes.White;
        Resources["SelectedInk"] = Resources["Ink"];
        if (SystemParameters.HighContrast)
        {
            Resources["Surface"] = SystemColors.WindowBrush; Resources["Card"] = SystemColors.WindowBrush;
            Resources["Ink"] = SystemColors.WindowTextBrush; Resources["MutedInk"] = SystemColors.WindowTextBrush;
            Resources["Line"] = SystemColors.WindowTextBrush; Resources["Accent"] = SystemColors.HighlightBrush; Resources["Selected"] = SystemColors.HighlightBrush;
            Resources["AccentInk"] = SystemColors.HighlightTextBrush; Resources["SelectedInk"] = SystemColors.HighlightTextBrush;
        }
    }
}
