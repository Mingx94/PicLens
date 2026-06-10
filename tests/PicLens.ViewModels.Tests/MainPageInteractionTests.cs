namespace PicLens.ViewModels.Tests;

public sealed class MainPageInteractionTests
{
    [Fact]
    public void Library_double_tap_defers_open_until_input_event_finishes()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "PicLens", "MainPage.xaml.cs"));
        var handlerStart = code.IndexOf("LibraryGrid_DoubleTapped", StringComparison.Ordinal);
        Assert.True(handlerStart >= 0, "Could not find LibraryGrid_DoubleTapped.");

        var nextHandlerStart = code.IndexOf("    private void QueueOpenLibraryItemFromDoubleTap", handlerStart, StringComparison.Ordinal);
        if (nextHandlerStart < 0)
        {
            nextHandlerStart = code.IndexOf("LibraryGrid_KeyDown", handlerStart, StringComparison.Ordinal);
        }

        Assert.True(nextHandlerStart > handlerStart, "Could not find the end of LibraryGrid_DoubleTapped.");

        var handler = code[handlerStart..nextHandlerStart];
        var handledIndex = handler.IndexOf("e.Handled = true;", StringComparison.Ordinal);
        var queueIndex = handler.IndexOf("QueueOpenLibraryItemFromDoubleTap(item);", StringComparison.Ordinal);

        Assert.True(handledIndex >= 0, "Double-tap must be handled before opening a secondary window.");
        Assert.True(queueIndex >= 0, "Double-tap open should be deferred until WinUI finishes input focus processing.");
        Assert.True(handledIndex < queueIndex, "Double-tap should mark the routed event handled before queuing the open.");
        Assert.DoesNotContain("await ViewModel.OpenLibraryItemAsync(item);", handler);
        Assert.Contains("App.DispatcherQueue.TryEnqueue", code);
    }

    [Fact]
    public void Opening_viewer_makes_secondary_window_owned_and_foreground()
    {
        var pageCode = File.ReadAllText(Path.Combine(RepositoryRoot(), "PicLens", "MainPage.xaml.cs"));
        var foregroundCode = File.ReadAllText(Path.Combine(RepositoryRoot(), "PicLens", "WindowForeground.cs"));

        Assert.Contains("WindowForeground.ActivateOwnedWindow(App.Window, window);", pageCode);
        Assert.DoesNotContain("window.Activate();", pageCode);
        Assert.Contains("GWLP_HWNDPARENT", foregroundCode);
        Assert.Contains("SetWindowLong", foregroundCode);
        Assert.Contains("SetForegroundWindow", foregroundCode);
        Assert.Contains("HWND_TOPMOST", foregroundCode);
        Assert.Contains("HWND_NOTOPMOST", foregroundCode);
        Assert.Contains("window.DispatcherQueue.TryEnqueue", foregroundCode);
    }

    [Fact]
    public void Secondary_viewer_records_lifecycle_breadcrumbs_and_bitmap_binding_failures()
    {
        var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "PicLens", "ImageViewerWindow.xaml.cs"));

        Assert.Contains("ImageViewerWindow constructing.", code);
        Assert.Contains("ImageViewerWindow InitializeComponent completed.", code);
        Assert.Contains("ImageViewerWindow constructed.", code);
        Assert.Contains("ImageViewerWindow loaded.", code);
        Assert.Contains("Create viewer bitmap image failed.", code);
        Assert.Contains("Image viewer fullscreen changed.", code);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PicLens.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
