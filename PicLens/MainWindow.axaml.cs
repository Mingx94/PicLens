using Avalonia.Controls;

namespace PicLens;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetViewerTitle(string? imageName)
    {
        Title = string.IsNullOrWhiteSpace(imageName)
            ? "PicLens"
            : $"PicLens - {imageName}";
    }
}
