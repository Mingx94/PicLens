using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace PicLens.App;

/// <summary>Diagnostic entry point; no source files are opened or modified.</summary>
public sealed class ComponentWindow : Window
{
    readonly ComboBox sorting = new() { Width = 200, SelectedIndex = 0, Margin = new Thickness(0, 0, 12, 0) };
    public ComponentWindow()
    {
        Title = "PicLens · 元件展示";
        Width = 800; Height = 800; MinWidth = 600; MinHeight = 500;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        var body = new StackPanel { Margin = new Thickness(28) };
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.SetResourceReference(BackgroundProperty, "Surface");
        Content = scroll;
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
        var icons = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        foreach (var kind in Enum.GetValues<Controls.IconKind>())
            icons.Children.Add(new Controls.LucideIcon { Kind = kind, Width = 24, Height = 24, Margin = new Thickness(6) });
        body.Children.Add(icons);
        var buttons = new WrapPanel { Margin = new Thickness(0, 24, 0, 16) };
        buttons.Children.Add(new Button { Content = "一般動作" });
        var primary = new Button { Content = "主要動作" };
        primary.SetResourceReference(BackgroundProperty, "Accent"); primary.SetResourceReference(ForegroundProperty, "AccentInk"); buttons.Children.Add(primary);
        buttons.Children.Add(new Button { Content = "無法使用", IsEnabled = false }); body.Children.Add(buttons);
        body.Children.Add(new TextBox { Text = "繁體中文輸入 · 長檔名_2026_圖片範例.png", Margin = new Thickness(0, 0, 0, 14) });
        var choices = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (string label in new[] { "名稱：由小到大", "名稱：由大到小", "修改時間：最舊", "修改時間：最新" }) sorting.Items.Add(label);
        choices.Children.Add(sorting);
        choices.Children.Add(new ComboBox { Width = 200, ItemsSource = new[] { "無法使用的選單" }, SelectedIndex = 0, IsEnabled = false });
        body.Children.Add(choices);
        var checks = new WrapPanel();
        checks.Children.Add(new CheckBox { Content = "未勾選", IsChecked = false });
        checks.Children.Add(new CheckBox { Content = "已勾選", IsChecked = true });
        checks.Children.Add(new CheckBox { Content = "部分勾選", IsThreeState = true, IsChecked = null });
        checks.Children.Add(new CheckBox { Content = "停用", IsChecked = true, IsEnabled = false });
        body.Children.Add(checks);
        var sliders = new WrapPanel { Margin = new Thickness(0, 10, 0, 10) };
        foreach (double value in new[] { 120d, 160d, 240d })
            sliders.Children.Add(new Slider { Width = 160, Minimum = 120, Maximum = 240, Value = value, Margin = new Thickness(0, 0, 18, 0) });
        sliders.Children.Add(new Slider { Width = 120, Minimum = 120, Maximum = 240, Value = 180, IsEnabled = false });
        body.Children.Add(sliders);
        foreach (string state in new[] { "尚未選擇資料夾。", "正在讀取資料夾…", "找不到符合條件的圖片。", "無法載入圖片；仍可選取、重新命名或移至回收筒。" })
            body.Children.Add(new TextBlock { Text = state, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
    }

    internal async Task CaptureStates(string path, Action<string, FrameworkElement> capture)
    {
        sorting.Focus();
        UpdateLayout();
        await Task.Delay(100);
        capture(path + ".components.png", (FrameworkElement)Content);
        sorting.IsDropDownOpen = true;
        await Task.Delay(100);
        if (sorting.Template.FindName("PART_Popup", sorting) is System.Windows.Controls.Primitives.Popup { Child: FrameworkElement menu })
            capture(path + ".dropdown.png", menu);
        sorting.IsDropDownOpen = false;
    }
}
