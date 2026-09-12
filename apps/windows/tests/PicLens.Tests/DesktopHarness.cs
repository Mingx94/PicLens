using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PicLens.App;
using PicLens.App.Controls;
using PicLens.Core;
using PicLens.Services;

namespace PicLens.Tests;

// Application resources and shutdown belong to one dispatcher in a separate process.
public static class DesktopHarness
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is not ["--desktop-check"]) return 2;
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(Fixture.Repo, "apps/windows/src/PicLens.App/App.xaml"));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = document.Descendants(ns + "ResourceDictionary").First();
        resources.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        foreach (var source in resources.Descendants().SelectMany(e => e.Attributes("Source")))
            source.Value = "/PicLens;component/" + source.Value;
        application.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString());
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        int exitCode = 1;
        dispatcher.BeginInvoke(async () =>
        {
            try { await CheckWindow(); exitCode = 0; Console.WriteLine("Desktop checks passed"); }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { dispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
        return exitCode;
    }

    static async Task Until(Func<bool> ready)
    {
        var timeout = Stopwatch.StartNew();
        while (!ready() && timeout.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
        Assert.True(ready(), "Desktop condition timed out");
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    static DragEventArgs DragArgs(IDataObject data, DependencyObject target, Point point) =>
        (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [data, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, target, point], null)!;

    static async Task CheckWindow()
    {
        using var fixture = new Fixture();
        var emptyProfile = new Profile(Path.Combine(fixture.Root, "empty-profile"));
        var emptyWindow = new MainWindow(emptyProfile, new LaunchOptions()) { ShowInTaskbar = false };
        bool emptyClosed = false; emptyWindow.Closed += (_, _) => emptyClosed = true;
        emptyWindow.Show();
        await Until(() => emptyWindow.IsLoaded);
        Assert.Equal(Visibility.Visible, ((FrameworkElement)emptyWindow.FindName("EmptyState")).Visibility);
        Assert.Equal("", emptyWindow.Model.RootPath); Assert.Empty(emptyWindow.Model.Roots);
        Assert.Contains(Descendants(emptyWindow).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "選擇資料夾");
        emptyWindow.Close(); await Until(() => emptyClosed);
        Assert.Contains("正常關閉", File.ReadAllText(emptyProfile.LogPath));

        string root = Path.Combine(fixture.Root, "library"); Directory.CreateDirectory(root);
        string source = fixture.Image("library/a.png"); fixture.Image("library/child/b.png"); fixture.Image("library/child/grandchild/c.png");
        var profile = new Profile(Path.Combine(fixture.Root, "profile"));
        string oldSettings = fixture.File("old-profile/piclens-settings.json", System.Text.Json.JsonSerializer.Serialize(new
        {
            lastFolderPath = root, sort = new { key = 0, direction = 0 }, includeSubfolders = false,
            thumbnailSize = 180, sidebarCollapsed = false, windowWidth = 800, windowHeight = 600
        }));
        byte[] oldHash = SHA256.HashData(File.ReadAllBytes(oldSettings));
        File.Copy(oldSettings, profile.SettingsPath);
        var window = new MainWindow(profile, new LaunchOptions()) { ShowInTaskbar = false };
        Assert.Equal(1600, window.Width); Assert.Equal(1000, window.Height);
        bool closed = false; window.Closed += (_, _) => closed = true;
        window.Show();
        try
        {
            await Until(() => window.IsLoaded && window.Model.Folder == root && !window.Model.Loading);
            Assert.InRange(window.ActualWidth, 800, 1600); Assert.InRange(window.ActualHeight, 600, 1000);
            Assert.Equal(180, window.Model.ThumbnailSize); Assert.Equal(root, window.Model.RootPath);
            Assert.Equal(root, window.Model.Settings.LastFolderPath);
            await window.Model.Pick(root);
            var gallery = (ListBox)window.FindName("Gallery");
            var tree = (TreeView)window.FindName("FolderTree");
            window.Activate(); tree.Focus();
            Assert.True(tree.IsKeyboardFocusWithin);
            var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(tree), Environment.TickCount, Key.Right)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            tree.RaiseEvent(key);
            Assert.Empty(window.Model.Selection.Ordered);
            gallery.Focus(); Assert.True(gallery.IsKeyboardFocusWithin);
            gallery.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(gallery), Environment.TickCount, Key.Right)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Assert.Equal(source, Assert.Single(window.Model.Selection.Ordered));

            for (int i = 0; i < 120; i++) File.Copy(source, Path.Combine(root, $"image-{i:000}.png"));
            await window.Model.Navigate(root, false); window.UpdateLayout();
            var first = window.Model.Items.First(t => !t.IsFolder);
            window.Model.Select(first, false, false); window.UpdateLayout();
            void AssertSelection()
            {
                Assert.Equal(window.Model.Selection.Ordered.Order(), gallery.SelectedItems.Cast<TileModel>().Select(t => t.Path).Order());
                foreach (var tile in window.Model.Items) Assert.Equal(window.Model.Selection.Contains(tile.Path), tile.Selected);
            }
            AssertSelection();
            // Regression: extending left twice must move the cursor, not stick to the range's last item.
            var selectable = window.Model.Items.Where(t => !t.IsFolder).Take(4).ToArray();
            window.Model.Select(selectable[3], false, false);
            Assert.Same(selectable[2], window.Model.MoveSelection(-1, false, true));
            Assert.Same(selectable[1], window.Model.MoveSelection(-1, false, true));
            Assert.Equal(selectable.Skip(1).Select(t => t.Path), window.Model.Selection.Ordered);
            Assert.Same(selectable[2], window.Model.MoveSelection(1, false, true));
            Assert.Equal(selectable.Skip(2).Select(t => t.Path), window.Model.Selection.Ordered);
            window.Model.Select(first, false, false);
            AssertSelection();
            var panel = MainWindow.FindVisual<VirtualizingTilePanel>(gallery)!;
            var scrollbar = Descendants(gallery).OfType<System.Windows.Controls.Primitives.ScrollBar>().Single(b => b.Orientation == Orientation.Vertical);
            scrollbar.ApplyTemplate();
            var track = (System.Windows.Controls.Primitives.Track)scrollbar.Template.FindName("PART_Track", scrollbar);
            Assert.NotNull(track.Thumb);
            System.Windows.Controls.Primitives.ScrollBar.PageDownCommand.Execute(null, scrollbar);
            window.UpdateLayout(); Assert.True(panel.VerticalOffset > 0);
            double offsetBeforeDrag = panel.VerticalOffset;
            track.Thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(0, 15)
            { RoutedEvent = System.Windows.Controls.Primitives.Thumb.DragDeltaEvent });
            window.UpdateLayout(); Assert.True(panel.VerticalOffset > offsetBeforeDrag);
            panel.SetVerticalOffset(100000); window.UpdateLayout(); AssertSelection();
            panel.SetVerticalOffset(0); window.UpdateLayout(); AssertSelection();
            var targetBorder = Descendants(gallery).OfType<Border>().First(b => b.Name == "Tile" && b.DataContext is TileModel t && !t.IsFolder && t.Path != first.Path);
            var dragOver = DragArgs(new DataObject("PicLens.Images", new[] { first.Path }), targetBorder, new Point(1, gallery.ActualHeight));
            dragOver.RoutedEvent = DragDrop.DragOverEvent;
            targetBorder.RaiseEvent(dragOver);
            var targetTile = (TileModel)targetBorder.DataContext;
            Assert.Equal(DragDropEffects.Move, dragOver.Effects); Assert.True(panel.VerticalOffset > 0);
            Assert.Contains(targetTile.Name, ((TextBlock)window.FindName("DragHintText")).Text);
            Assert.Same(window.FindResource("Accent"), targetBorder.BorderBrush);
            var invalidDrop = DragArgs(new DataObject("PicLens.Images", new[] { targetTile.Path }), targetBorder, new Point(1, 1));
            invalidDrop.RoutedEvent = DragDrop.DragOverEvent;
            targetBorder.RaiseEvent(invalidDrop);
            Assert.Equal(DragDropEffects.None, invalidDrop.Effects);
            Assert.Equal(0, ((System.Windows.Media.SolidColorBrush)targetBorder.BorderBrush).Color.A);
            Assert.Contains("放到其他圖片上", ((TextBlock)window.FindName("DragHintText")).Text);
            ((FrameworkElement)window.FindName("DragHint")).Visibility = Visibility.Visible;
            typeof(MainWindow).GetField("pressedTile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, first);
            gallery.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });
            Assert.Null(typeof(MainWindow).GetField("pressedTile", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window));
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("DragHint")).Visibility);
            window.Model.ThumbnailSize = 240; window.UpdateLayout(); AssertSelection();
            Assert.Equal(first.Path, Assert.Single(window.Model.Selection.Ordered));
            Assert.True(gallery.IsKeyboardFocusWithin);
            var search = (TextBox)window.FindName("SearchBox");
            search.Focus(); search.Text = "image-001";
            await Until(() => window.Model.Items.Count == 1);
            Assert.Empty(window.Model.Selection.Ordered); AssertSelection(); Assert.True(search.IsKeyboardFocusWithin);
            typeof(MainWindow).GetMethod("ClearSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [search, new RoutedEventArgs()]);
            Assert.True(search.IsKeyboardFocusWithin); Assert.Equal(122, window.Model.Items.Count);
            gallery.Focus(); window.Model.Select(window.Model.Items.First(t => !t.IsFolder), false, false);
            window.Model.SortIndex = 1; window.UpdateLayout(); Assert.Empty(window.Model.Selection.Ordered); AssertSelection();
            Assert.True(gallery.IsKeyboardFocusWithin);
            await window.Model.Navigate(Path.Combine(root, "child"));
            Assert.Empty(window.Model.Selection.Ordered); AssertSelection(); Assert.Equal(root, window.Model.RootPath);

            var rootNode = Assert.Single(window.Model.Roots);
            Assert.False(rootNode.Loaded);
            tree.UpdateLayout();
            var childContainer = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(rootNode);
            childContainer.IsExpanded = true;
            await Until(() => rootNode.Loaded);
            Assert.Equal(Path.Combine(root, "child", "grandchild"), Assert.Single(rootNode.Children).Path);
            childContainer.IsExpanded = false; Assert.False(childContainer.IsExpanded);
            childContainer.IsExpanded = true; Assert.True(childContainer.IsExpanded);
            void SideButton(MouseButton button) => window.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
            SideButton(MouseButton.XButton1);
            await Until(() => window.Model.Folder == root && !window.Model.Loading);
            SideButton(MouseButton.XButton2);
            await Until(() => window.Model.Folder == Path.Combine(root, "child") && !window.Model.Loading);
            Assert.Same(rootNode, Assert.Single(window.Model.Roots)); Assert.Equal(root, window.Model.RootPath);
            typeof(MainWindow).GetMethod("RootClicked", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            await Until(() => window.Model.Folder == root && !window.Model.Loading);
            Assert.Same(rootNode, Assert.Single(window.Model.Roots));
            Assert.Contains(Descendants(window).OfType<Button>(), b => System.Windows.Automation.AutomationProperties.GetName(b) == "根資料夾（固定展開）");
            await window.Model.FlushSettingsAsync(); Assert.Equal(root, profile.Load().LastFolderPath);

            Exception? dialogFailure = null;
            void CancelDialog(Action<Window> inspect)
            {
                window.Dispatcher.BeginInvoke(() =>
                {
                    var dialog = window.OwnedWindows.Cast<Window>().Single(w => w.IsVisible);
                    try { inspect(dialog); }
                    catch (Exception ex) { dialogFailure = ex; }
                    finally { dialog.Close(); }
                }, DispatcherPriority.ApplicationIdle);
            }
            gallery.Focus();
            CancelDialog(dialog =>
            {
                var field = Descendants(dialog).OfType<TextBox>().Single();
                var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(field)!;
                Assert.Equal("新檔名（不含副檔名）", peer.GetName());
                Assert.Equal(System.Windows.Automation.Peers.AutomationControlType.Edit, peer.GetAutomationControlType());
                Assert.Equal("範例名稱", field.SelectedText);
            });
            Assert.Null(Dialogs.Rename(window, "範例名稱"));
            if (dialogFailure is not null) throw dialogFailure;
            Assert.True(gallery.IsKeyboardFocusWithin);
            CancelDialog(dialog =>
            {
                Assert.Contains(Descendants(dialog).OfType<TextBlock>(), t => t.Text.Contains("2 張"));
                Assert.Contains(Descendants(dialog).OfType<Button>(), b => b.IsCancel && b.IsDefault);
                var list = Descendants(dialog).OfType<ListBox>().Single();
                var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(list)!;
                Assert.Equal("待處理檔案", peer.GetName()); Assert.Equal(2, list.Items.Count);
            });
            Assert.False(Dialogs.Confirm(window, "取消測試", "將處理 2 張圖片", ["a.png", "b.png"]));
            if (dialogFailure is not null) throw dialogFailure;
            Assert.True(gallery.IsKeyboardFocusWithin);

            var methods = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(MainWindow).GetMethod("OpenViewer", methods)!.Invoke(window, [source]);
            var canvas = (ViewerCanvas)window.FindName("Canvas");
            await Until(() => canvas.Original);
            File.Delete(source);
            typeof(MainWindow).GetMethod("Reveal", methods)!.Invoke(window, [source]);
            Assert.Contains("檔案已不存在", window.Model.Status);
            Assert.True(canvas.Original);
            var currentViewer = (ViewerController)typeof(MainWindow).GetField("viewer", methods)!.GetValue(window)!;
            Assert.True(currentViewer.IsOpen); Assert.Equal(source, currentViewer.CurrentPath);
            typeof(MainWindow).GetMethod("CloseViewerCore", methods)!.Invoke(window, null);
            Assert.True(gallery.IsKeyboardFocusWithin);

            // Many tiny files keep work pending while the real Closing handler cancels it.
            var plans = Enumerable.Range(0, 256).Select(i =>
            {
                string path = fixture.File($"batch/{i}.png", $"fixture-{i}");
                return new FilePlan(path, Path.ChangeExtension(path, ".renamed.png"), OperationKind.Rename, FileStamp.Read(path));
            }).ToArray();
            var hashes = plans.Select(p => SHA256.HashData(File.ReadAllBytes(p.Source))).ToArray();
            window.Model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LibraryViewModel.Status) && window.Model.Status.StartsWith("正在處理 2 /", StringComparison.Ordinal))
                    window.Close();
            };
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var task = (Task)typeof(MainWindow).GetMethod("RunBatch", flags)!.Invoke(window, [plans])!;
            await task; await Until(() => closed);
            var result = Assert.IsType<BatchResult>(typeof(MainWindow).GetField("lastBatch", flags)!.GetValue(window));
            Assert.InRange(result.Succeeded, 1, plans.Length - 1); Assert.True(result.Canceled > 0); Assert.Equal(0, result.Failed);
            Assert.Equal(plans.Length, result.Items.Count);
            for (int i = 0; i < plans.Length; i++)
            {
                bool succeeded = result.Items[i].Status == ResultStatus.Succeeded;
                Assert.True(succeeded || result.Items[i].Status == ResultStatus.Canceled);
                Assert.Equal(!succeeded, File.Exists(plans[i].Source));
                Assert.Equal(succeeded, File.Exists(plans[i].Target));
                Assert.Equal(hashes[i], SHA256.HashData(File.ReadAllBytes(succeeded ? plans[i].Target! : plans[i].Source)));
            }
            Assert.Empty(Directory.GetFiles(profile.Temporary));
            Assert.Empty(Directory.GetFiles(Path.Combine(fixture.Root, "batch"), ".piclens-*.tmp"));
            Assert.Equal(0, ((WorkerPool)typeof(MainWindow).GetField("workers", flags)!.GetValue(window)!).ActiveCount);
            Assert.Contains("正常關閉", File.ReadAllText(profile.LogPath));
            // A planning continuation arriving after Closing must not start another batch.
            string lateSource = fixture.File("late.png", "preserve after close");
            var latePlan = new FilePlan(lateSource, Path.Combine(fixture.Root, "late-renamed.png"), OperationKind.Rename, FileStamp.Read(lateSource));
            await (Task)typeof(MainWindow).GetMethod("RunBatch", flags)!.Invoke(window, [new[] { latePlan }])!;
            Assert.Equal("preserve after close", File.ReadAllText(lateSource)); Assert.False(File.Exists(latePlan.Target));
            Assert.Equal(oldHash, SHA256.HashData(File.ReadAllBytes(oldSettings)));
            var saved = profile.Load(); Assert.Equal(root, saved.LastFolderPath);
            Assert.Equal(240, saved.ThumbnailSize); Assert.Equal(1, saved.Sort.Direction);
            Assert.Empty(Directory.GetFiles(profile.Root, "*.corrupt.*"));
            Assert.Empty(Directory.GetFiles(profile.Root, "*.tmp"));
        }
        finally { if (!closed) { window.Close(); await Until(() => closed); } }
    }

}
