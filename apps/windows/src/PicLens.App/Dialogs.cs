using System.Windows;
using System.Windows.Controls;
using PicLens.Core;
namespace PicLens.App;

public static class Dialogs
{
    static Window Create(Window owner, string title)
    {
        return new Window
        {
            Owner = owner,
            Title = title,
            Width = 660,
            Height = 430,
            MinWidth = 450,
            MinHeight = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
    }
    public static bool Confirm(Window owner, string title, string explanation, IReadOnlyList<string> rows)
    {
        var dialog = Create(owner, title);
        var grid = new DockPanel { Margin = new Thickness(24) };
        var header = new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 18), FontSize = 16 };
        DockPanel.SetDock(header, Dock.Top); grid.Children.Add(header);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 18, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true, IsDefault = true };
        var confirm = new Button { Content = "確認執行" };
        confirm.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(confirm); DockPanel.SetDock(buttons, Dock.Bottom); grid.Children.Add(buttons);
        var list = new ListBox { ItemsSource = rows };
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        grid.Children.Add(list); dialog.Content = grid;
        return dialog.ShowDialog() == true;
    }
    public static string? Rename(Window owner, string current)
    {
        var dialog = Create(owner, "重新命名"); dialog.Height = 230;
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock { Text = "只修改檔名，副檔名保持不變。", Margin = new(0, 0, 0, 16) });
        var field = new TextBox { Text = current }; stack.Children.Add(field);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 18, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true, IsDefault = true };
        var okay = new Button { Content = "重新命名" }; okay.Click += (_, _) => dialog.DialogResult = true;
        buttons.Children.Add(cancel); buttons.Children.Add(okay); stack.Children.Add(buttons); dialog.Content = stack;
        dialog.Loaded += (_, _) => { field.Focus(); field.SelectAll(); };
        return dialog.ShowDialog() == true ? field.Text : null;
    }
    public static void Results(Window owner, BatchResult batch)
    {
        var dialog = Create(owner, "操作結果");
        var panel = new DockPanel { Margin = new Thickness(20) };
        var title = new TextBlock { Text = batch.Summary, Margin = new(0, 0, 0, 12) }; DockPanel.SetDock(title, Dock.Top); panel.Children.Add(title);
        var close = new Button { Content = "關閉", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 12, 0, 0) };
        DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        string Label(ResultStatus status) => status switch { ResultStatus.Succeeded => "成功", ResultStatus.Skipped => "略過", ResultStatus.Canceled => "取消", ResultStatus.Unknown => "結果待確認", _ => "失敗" };
        var list = new ListBox { ItemsSource = batch.Items.Select(r => $"{Label(r.Status)} · {r.Source}\n→ {r.Target ?? "回收筒"}\n{r.Message}") };
        panel.Children.Add(list); dialog.Content = panel; dialog.ShowDialog();
    }
}
