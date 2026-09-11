using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace PicLens.App;

/// <summary>Diagnostic entry point; no source files are opened or modified.</summary>
public sealed class ComponentWindow : Window
{
    public ComponentWindow()
    {
        Title = "PicLens · 元件展示";
        Width = 800; Height = 640; MinWidth = 600; MinHeight = 500;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        var body = new StackPanel { Margin = new Thickness(28) };
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(new TextBlock { Text = "PicLens 元件與狀態", FontSize = 28, Margin = new Thickness(0, 0, 0, 24) });
        var palette = new WrapPanel();
        foreach (string role in new[] { "Surface", "Card", "Ink", "MutedInk", "Line", "Accent", "Selected" })
        {
            var swatch = new Border { Width = 78, Height = 48, Margin = new Thickness(4), CornerRadius = new CornerRadius(6) };
            swatch.SetResourceReference(BackgroundProperty, role);
            var group = new StackPanel(); group.Children.Add(swatch); group.Children.Add(new TextBlock { Text = role, HorizontalAlignment = HorizontalAlignment.Center });
            palette.Children.Add(group);
        }
        body.Children.Add(palette);
        var buttons = new WrapPanel { Margin = new Thickness(0, 24, 0, 16) };
        buttons.Children.Add(new Button { Content = "一般動作" });
        var primary = new Button { Content = "主要動作" };
        primary.SetResourceReference(BackgroundProperty, "Accent"); primary.SetResourceReference(ForegroundProperty, "AccentInk"); buttons.Children.Add(primary);
        buttons.Children.Add(new Button { Content = "無法使用", IsEnabled = false }); body.Children.Add(buttons);
        body.Children.Add(new TextBox { Text = "繁體中文輸入 · 長檔名_2026_圖片範例.png", Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(new CheckBox { Content = "含子資料夾", IsChecked = true });
        body.Children.Add(new Slider { Minimum = 120, Maximum = 240, Value = 160, Margin = new Thickness(0, 14, 0, 14) });
        foreach (string state in new[] { "尚未選擇資料夾。", "正在讀取資料夾…", "找不到符合條件的圖片。", "無法載入圖片；仍可選取、重新命名或移至回收筒。" })
            body.Children.Add(new TextBlock { Text = state, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
    }
}
