using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PicLens.Services;

namespace PicLens;

internal static class DropRenamePreviewContent
{
    public static UIElement Create(DropRenamePreview preview)
    {
        var panel = new StackPanel
        {
            MaxWidth = 560,
            Spacing = 12
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"將重新命名 {preview.RenameCount} 個，略過 {preview.SkippedCount} 個。",
            TextWrapping = TextWrapping.Wrap,
            Style = TryGetResource<Style>("BodyStrongTextBlockStyle")
        });

        var rows = new StackPanel { Spacing = 8 };
        foreach (var item in preview.Items.Take(12))
        {
            rows.Children.Add(CreateRow(item));
        }

        if (preview.Items.Count > 12)
        {
            rows.Children.Add(new TextBlock
            {
                Text = $"另有 {preview.Items.Count - 12} 個項目。",
                TextWrapping = TextWrapping.Wrap,
                Foreground = TryGetResource<Brush>("TextFillColorSecondaryBrush"),
                Style = TryGetResource<Style>("CaptionTextBlockStyle")
            });
        }

        panel.Children.Add(rows);

        return new ScrollViewer
        {
            MaxHeight = 360,
            Content = panel
        };
    }

    private static UIElement CreateRow(DropRenamePreviewItem item)
    {
        var row = new Grid
        {
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4, 0, 4)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var icon = new FontIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            Glyph = item.WillRename ? "\uE8FB" : "\uE711",
            FontFamily = TryGetResource<FontFamily>("SymbolThemeFontFamily")
        };
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var text = new TextBlock
        {
            Text = item.WillRename
                ? $"{item.SourceName} -> {item.TargetName}"
                : $"{item.SourceName} ({ReasonText(item.Reason)})",
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        return row;
    }

    private static string ReasonText(string? reason) =>
        reason switch
        {
            "already_target_sequence" => "已是目標序列名稱",
            _ => reason ?? "略過"
        };

    private static T? TryGetResource<T>(string resourceKey)
        where T : class =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as T
            : null;
}
