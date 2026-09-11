using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using PicLens.Core;
using PicLens.Services;
using PicLens.App.Controls;
namespace PicLens.App;

public partial class MainWindow : Window
{
    readonly Profile profile; readonly LaunchOptions options; readonly WorkerPool workers; readonly ImageService images;
    readonly ViewerController viewer; readonly FileOperations files;
    CancellationTokenSource? batchCancellation; Task? batchTask;
    BatchResult? lastBatch; readonly DispatcherTimer toastTimer = new(); bool closing, closed;
    Point pressed; TileModel? pressedTile; bool dragging; Border? dragTarget;
    readonly Stopwatch lifetime = Stopwatch.StartNew(); readonly List<double> sharpSamples = [];
    int unpainted, maxRealizedContainers; double libraryMs, searchMs; bool exerciseComplete;
    public LibraryViewModel Model { get; }
    public MainWindow(Profile profile, LaunchOptions options)
    {
        this.profile = profile; this.options = options;
        workers = new(profile); images = new(profile, workers); images.StartCleanup(); files = new(profile, workers);
        Model = new(profile, images);
        InitializeComponent(); DataContext = Model;
        Width = options.Width; Height = options.Height;
        viewer = new(images, profile, Canvas);
        viewer.StateChanged += (name, info) => { HeaderTitle.Text = name; ViewerInfo.Text = info; };
        viewer.SharpPaint += ms => sharpSamples.Add(ms); viewer.Unpainted += () => unpainted++;
        Canvas.ZoomChanged += scale => ZoomButton.Content = $"{scale:P0}";
        Model.LibraryMeasured += ms => libraryMs = ms; Model.SearchMeasured += ms => searchMs = ms;
        Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Model.Count)) EmptyState.Visibility = Model.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (e.PropertyName == nameof(Model.SidebarCollapsed)) UpdateSidebar();
        };
        toastTimer.Tick += (_, _) => { Toast.Visibility = Visibility.Collapsed; toastTimer.Stop(); };
        UpdateSidebar();
        if (options.Metrics is not null) Gallery.LayoutUpdated += (_, _) => {
            maxRealizedContainers = Math.Max(maxRealizedContainers, FindVisual<VirtualizingTilePanel>(Gallery)?.RealizedCount ?? 0);
        };
        Loaded += async (_, _) =>
        {
            string? initial = options.Folder ?? Model.Settings.LastFolderPath;
            if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial)) await Model.Pick(initial, persist: false);
            if (closing) return;
            if (options.Components) new ComponentWindow { Owner = this }.Show();
            if (options.Viewer is not null) OpenViewer(options.Viewer);
            if (options.Exercise) _ = Exercise();
            if (options.Screenshot is not null)
            {
                await Task.Delay(1800); if (!closing) await Dispatcher.InvokeAsync(() => Screenshot(options.Screenshot), DispatcherPriority.ApplicationIdle);
            }
            if (options.SmokeMs is int timeout) { await Task.Delay(timeout); if (!closing) Close(); }
        };
    }
    void UpdateSidebar() { if (SidebarColumn is null) return; SidebarColumn.Width = new(Model.SidebarCollapsed ? 0 : ActualWidth < 1000 ? 170 : 220); }
    void WindowSizeChanged(object sender, SizeChangedEventArgs e) => UpdateSidebar();
    async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        if (batchTask is { IsCompleted: false }) return;
        var dialog = new OpenFolderDialog { Title = "選擇圖片資料夾", Multiselect = false };
        if (dialog.ShowDialog(this) == true) { CloseViewerCore(); await Model.Pick(dialog.FolderName); }
    }
    async void RootClicked(object sender, RoutedEventArgs e) => await Model.Navigate(Model.RootPath);
    async void BackClicked(object sender, RoutedEventArgs e) { if (Model.History.Back() is string path) await Model.Navigate(path, false); }
    async void ForwardClicked(object sender, RoutedEventArgs e) { if (Model.History.Forward() is string path) await Model.Navigate(path, false); }
    async void RefreshClicked(object sender, RoutedEventArgs e) => await Model.Navigate(Model.Folder, false);
    void ClearSearch(object sender, RoutedEventArgs e) { Model.Search = ""; SearchBox.Focus(); }
    void ToggleSidebar(object sender, RoutedEventArgs e) => Model.SidebarCollapsed = !Model.SidebarCollapsed;
    async void FolderExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: FolderNode node }) await Model.Expand(node);
    }
    async void TreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNode node && !string.IsNullOrEmpty(node.Path)) await Model.Navigate(node.Path);
    }
    void TileLoaded(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is TileModel tile) Model.Thumbnails.Visible(tile); }
    void TileUnloaded(object sender, RoutedEventArgs e) { if (((FrameworkElement)sender).DataContext is TileModel tile) Model.Thumbnails.Hidden(tile); }
    void TileContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TileModel old) Model.Thumbnails.Hidden(old);
        if (e.NewValue is TileModel next && ((FrameworkElement)sender).IsLoaded) Model.Thumbnails.Visible(next);
    }
    async void TileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TileModel tile) return;
        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right)) return;
        e.Handled = true; Gallery.Focus();
        if (e.ChangedButton == MouseButton.Right)
        {
            if (tile.IsFolder) return;
            if (!Model.Selection.Contains(tile.Path)) Model.Select(tile, false, false);
            ShowMenu(tile); return;
        }
        if (tile.IsFolder) { await Model.Navigate(tile.Path); return; }
        pressed = e.GetPosition(Gallery); pressedTile = tile;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // Preserve an existing multi-selection until mouse release or drag threshold.
        if (!Model.Selection.Contains(tile.Path) || ctrl || shift) Model.Select(tile, ctrl, shift);
        if (e.ClickCount == 2) { OpenViewer(Model.Selection.Ordered.FirstOrDefault() ?? tile.Path); pressedTile = null; }
    }
    void TileMouseMove(object sender, MouseEventArgs e)
    {
        if (pressedTile is null || e.LeftButton != MouseButtonState.Pressed || dragging) return;
        var point = e.GetPosition(Gallery);
        if (Math.Abs(point.X - pressed.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - pressed.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragging = true;
        DragHint.Visibility = Visibility.Visible; DragHintText.Text = $"正在拖曳 {Model.Selection.Ordered.Count} 張圖片 · 放到目標圖片上重新命名";
        try { DragDrop.DoDragDrop(Gallery, new DataObject("PicLens.Images", Model.Selection.Ordered.ToArray()), DragDropEffects.Move); }
        finally { dragging = false; pressedTile = null; ClearDrag(); }
    }
    void ShowMenu(TileModel tile)
    {
        var menu = new ContextMenu();
        MenuItem Add(string title, RoutedEventHandler action) { var item = new MenuItem { Header = title }; item.Click += action; menu.Items.Add(item); return item; }
        Add("在檔案總管顯示", (_, _) => Reveal(tile.Path));
        Add("重新命名", async (_, _) => await RenameSelected()).IsEnabled = Model.Selection.Ordered.Count == 1;
        Add($"移至回收筒（{Model.Selection.Ordered.Count} 張）", async (_, _) => await TrashSelected());
        menu.Closed += (_, _) => Gallery.Focus(); menu.IsOpen = true;
    }
    void OpenViewer(string path)
    {
        var entries = Model.Items.Where(x => !x.IsFolder).Select(x => x.Entry).ToList();
        if (!entries.Any(e => StringComparer.OrdinalIgnoreCase.Equals(e.Path, path))) return;
        Model.Thumbnails.Pause(true); Workspace.Visibility = Visibility.Collapsed; ViewerLayer.Visibility = Visibility.Visible;
        viewer.Open(entries, path); Canvas.Focus();
    }
    void CloseViewerCore()
    {
        viewer.Close(); ViewerLayer.Visibility = Visibility.Collapsed; Workspace.Visibility = Visibility.Visible;
        HeaderTitle.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Model.FolderName)));
        Model.Thumbnails.Pause(false); Gallery.Focus();
    }
    void CloseViewer(object sender, RoutedEventArgs e) => CloseViewerCore();
    void PreviousImage(object sender, RoutedEventArgs e) => viewer.Navigate(-1);
    void NextImage(object sender, RoutedEventArgs e) => viewer.Navigate(1);
    void ZoomOut(object sender, RoutedEventArgs e) => Canvas.ChangeZoom(-1);
    void ZoomIn(object sender, RoutedEventArgs e) => Canvas.ChangeZoom(1);
    void ResetZoom(object sender, RoutedEventArgs e) => Canvas.ResetZoom();
    void RevealViewer(object sender, RoutedEventArgs e) { if (viewer.CurrentPath is string path) Reveal(path); }
    void Reveal(string path) { try { FileOperations.Reveal(path); } catch (Exception ex) { Model.Status = ex.Message; profile.Log("Reveal 失敗", ex); } }
    async void ConvertJpeg(object sender, RoutedEventArgs e) => await Convert(OperationKind.Jpeg);
    async void ConvertWebp(object sender, RoutedEventArgs e) => await Convert(OperationKind.Webp);
    async Task Convert(OperationKind kind)
    {
        if (batchTask is { IsCompleted: false }) return;
        try
        {
            var snapshot = Model.Items.Select(x => x.Entry).Where(x => !x.IsFolder).ToList();
            var plans = await Task.Run(() => FilePlans.Convert(snapshot, kind));
            if (plans.Count == 0) return;
            if (FilePlans.RequiresConversionConfirmation(plans.Count) && !Dialogs.Confirm(this, "確認格式轉換", $"將處理目前顯示的 {plans.Count} 張圖片。保留原檔，目標衝突時略過。", PlanRows(plans))) return;
            await RunBatch(plans);
        }
        catch (Exception ex) { Error(ex); }
    }
    async void CleanupClicked(object sender, RoutedEventArgs e)
    {
        if (batchTask is { IsCompleted: false }) return;
        try
        {
            var snapshot = Model.Items.Select(x => x.Entry).ToList();
            var plans = await Task.Run(() => FilePlans.Cleanup(snapshot));
            if (plans.Count == 0) { Model.Status = "沒有可清理的同名格式。"; return; }
            if (Dialogs.Confirm(this, "確認同名清理", $"將 {plans.Count} 張其他同名格式移至回收筒。JPG／JPEG 與 WebP 都會保留。", PlanRows(plans))) await RunBatch(plans);
        }
        catch (Exception ex) { Error(ex); }
    }
    async Task RenameSelected()
    {
        if (batchTask is { IsCompleted: false } || Model.Selection.Ordered.Count != 1) return;
        string source = Model.Selection.Ordered[0];
        string? name = Dialogs.Rename(this, Path.GetFileNameWithoutExtension(source));
        if (name is null) return;
        try
        {
            string target = FilePlans.ValidateRename(source, name);
            var stamp = await Task.Run(() => FileStamp.Read(source));
            await RunBatch([new(source, target, OperationKind.Rename, stamp)]);
        }
        catch (Exception ex) { Error(ex); }
    }
    async Task TrashSelected()
    {
        if (batchTask is { IsCompleted: false }) return;
        try
        {
            var sources = Model.Selection.Ordered.ToArray();
            var plans = await Task.Run(() => sources.Select(p => new FilePlan(p, null, OperationKind.Trash, FileStamp.Capture(p))).ToList());
            if (plans.Count > 0 && Dialogs.Confirm(this, "移至回收筒", $"確定將 {plans.Count} 張圖片移至 Windows 回收筒？", PlanRows(plans))) await RunBatch(plans);
        }
        catch (Exception ex) { Error(ex); }
    }
    static string[] PlanRows(IReadOnlyList<FilePlan> plans) => plans.Select(p => $"{Path.GetFileName(p.Source)} → {(p.Target is null ? "回收筒" : Path.GetFileName(p.Target))}{(p.Skip is null ? "" : " · " + p.Skip)}").ToArray();
    async Task RunBatch(IReadOnlyList<FilePlan> plans)
    {
        if (batchTask is { IsCompleted: false }) return;
        batchCancellation = new(); CancelBatchButton.Visibility = Visibility.Visible;
        batchTask = Execute();
        await batchTask;
        async Task Execute()
        {
            try
            {
                lastBatch = await files.ExecuteAsync(plans, new Progress<string>(s => Model.Status = s), batchCancellation.Token);
                if (!closing)
                {
                    await Model.Navigate(Model.Folder, false); Model.Status = lastBatch.Summary;
                    ToastMessage.Text = lastBatch.Summary; Toast.Visibility = Visibility.Visible;
                    toastTimer.Interval = TimeSpan.FromSeconds(lastBatch.Failed > 0 ? 12 : 6); toastTimer.Start();
                }
            }
            finally { CancelBatchButton.Visibility = Visibility.Collapsed; batchCancellation.Dispose(); batchCancellation = null; }
        }
    }
    void CancelBatch(object sender, RoutedEventArgs e) => batchCancellation?.Cancel();
    async void RetrySettingsSave(object sender, RoutedEventArgs e) => await Model.RetrySettingsSaveAsync();
    void ShowResults(object sender, RoutedEventArgs e) { if (lastBatch is not null) Dialogs.Results(this, lastBatch); }
    void DismissToast(object sender, RoutedEventArgs e) { Toast.Visibility = Visibility.Collapsed; toastTimer.Stop(); }
    void Error(Exception ex) { Model.Status = ex.Message; profile.Log("操作失敗", ex); }
    static Border? TileBorder(DependencyObject? source)
    {
        while (source is not null) { if (source is Border b && b.DataContext is TileModel && b.Name == "Tile") return b; source = VisualTreeHelper.GetParent(source); }
        return null;
    }
    void GalleryDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true; var target = TileBorder(e.OriginalSource as DependencyObject);
        e.Effects = e.Data.GetDataPresent("PicLens.Images") && target?.DataContext is TileModel { IsFolder: false } ? DragDropEffects.Move : DragDropEffects.None;
        if (dragTarget != target) { if (dragTarget is not null) ResetBorder(dragTarget); dragTarget = target; }
        if (e.Effects == DragDropEffects.Move && target is not null)
        {
            target.BorderBrush = Brushes.DarkOrange; DragHintText.Text = $"放到「{((TileModel)target.DataContext).Name}」· 預覽批次重新命名";
        }
        var p = e.GetPosition(Gallery); var panel = FindVisual<VirtualizingTilePanel>(Gallery);
        if (p.Y < 40) panel?.LineUp(); else if (p.Y > Gallery.ActualHeight - 40) panel?.LineDown();
    }
    async void GalleryDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (batchTask is { IsCompleted: false }) return;
        if (e.Data.GetData("PicLens.Images") is not string[] sources || TileBorder(e.OriginalSource as DependencyObject)?.DataContext is not TileModel { IsFolder: false } target) return;
        ClearDrag();
        try
        {
            var plans = await Task.Run(() => FilePlans.DropRename(sources, target.Path, Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(target.Path)!)));
            if (plans.Count > 0 && Dialogs.Confirm(this, "預覽批次重新命名", $"{plans.Count} 張圖片將依目標名稱編號。確認前不修改檔案；衝突時略過。", PlanRows(plans))) await RunBatch(plans);
        }
        catch (Exception ex) { Error(ex); }
    }
    void GalleryDragLeave(object sender, DragEventArgs e) { if (dragTarget is not null) { ResetBorder(dragTarget); dragTarget = null; } }
    static void ResetBorder(Border border) => border.ClearValue(Border.BorderBrushProperty);
    void ClearDrag() { if (dragTarget is not null) ResetBorder(dragTarget); dragTarget = null; DragHint.Visibility = Visibility.Collapsed; }
    public static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T found) return found; if (FindVisual<T>(child) is T nested) return nested; }
        return null;
    }
    void WindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (viewer.IsOpen) return;
        if (e.ChangedButton == MouseButton.XButton1) { BackClicked(sender, e); e.Handled = true; }
        if (e.ChangedButton == MouseButton.XButton2) { ForwardClicked(sender, e); e.Handled = true; }
    }
    void GallerySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Model.SyncingSelection) return;
        foreach (var tile in e.RemovedItems.OfType<TileModel>()) Model.Selection.Remove(tile.Path);
        var visible = Model.Items.Where(t => !t.IsFolder).Select(t => t.Path).ToList();
        foreach (var tile in e.AddedItems.OfType<TileModel>().Where(t => !t.IsFolder))
            if (!Model.Selection.Contains(tile.Path)) Model.Selection.Select(tile.Path, visible, true, false);
        Model.SyncSelection();
    }
    void WindowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && pressedTile is { } tile && !dragging)
        {
            if (Keyboard.Modifiers == ModifierKeys.None && !viewer.IsOpen) Model.Select(tile, false, false);
            pressedTile = null;
        }
    }
    async void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !viewer.IsOpen) { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; return; }
        if (viewer.IsOpen)
        {
            if (e.Key == Key.Escape) CloseViewerCore();
            else if (Canvas.CurrentZoom.Scale <= 1.01 && e.Key is Key.Left or Key.Right) viewer.Navigate(e.Key == Key.Right ? 1 : -1);
            else return;
            e.Handled = true; return;
        }
        if (Keyboard.FocusedElement is TextBox or ComboBox) return;
        if (e.Key == Key.F5) { await Model.Navigate(Model.Folder, false); e.Handled = true; }
        if (e.Key == Key.Escape) { Model.Selection.Clear(); Model.SyncSelection(); }
        if (e.Key == Key.Enter && Model.Selection.Ordered.FirstOrDefault() is string selected) { OpenViewer(selected); e.Handled = true; }
        if (e.Key == Key.F2) { await RenameSelected(); e.Handled = true; }
        if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
        {
            if (Model.Items.FirstOrDefault(t => t.Path == Model.Selection.Ordered.FirstOrDefault()) is { } tile) ShowMenu(tile);
            e.Handled = true;
        }
        if (e.Key == Key.Delete) { await TrashSelected(); e.Handled = true; }
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)
        {
            var images = Model.Items.Where(t => !t.IsFolder).ToList(); if (images.Count == 0) return;
            int current = images.FindIndex(t => t.Path == Model.Selection.Ordered.LastOrDefault());
            int columns = Math.Max(1, (int)(Gallery.ActualWidth / Model.TileWidth));
            int next = e.Key switch { Key.Home => 0, Key.End => images.Count - 1, Key.Left => current - 1, Key.Right => current + 1, Key.Up => current - columns, _ => current + columns };
            var tile = images[Math.Clamp(next, 0, images.Count - 1)];
            Model.Select(tile, Keyboard.Modifiers.HasFlag(ModifierKeys.Control), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            FindVisual<VirtualizingTilePanel>(Gallery)?.ScrollToIndex(Model.Items.IndexOf(tile)); e.Handled = true;
        }
    }
    void Screenshot(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var surface = (FrameworkElement)Content;
            var dpi = VisualTreeHelper.GetDpi(surface);
            var bitmap = new RenderTargetBitmap((int)(surface.ActualWidth * dpi.DpiScaleX), (int)(surface.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(surface); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(path); encoder.Save(file); profile.Log("截圖 " + path);
        }
        catch (Exception ex) { Error(ex); }
    }
    async Task Exercise()
    {
        await Task.Delay(700);
        if (closing) return;
        Model.Search = ".jpg"; await Task.Delay(300); Model.Search = "";
        var panel = FindVisual<VirtualizingTilePanel>(Gallery);
        for (int i = 0; i < 20; i++) { if (closing) return; panel?.LineDown(); await Task.Delay(35); }
        panel?.SetVerticalOffset(0);
        var first = Model.Items.FirstOrDefault(x => !x.IsFolder && !x.Entry.Animated);
        if (first is not null)
        {
            Model.Select(first, false, false); OpenViewer(first.Path);
            await Task.Delay(900);
            for (int i = 0; i < Math.Min(12, Model.Items.Count - 1); i++) { if (closing) return; viewer.Navigate(1); await Task.Delay(650); }
            for (int i = 0; i < Math.Min(12, Model.Items.Count - 1); i++) { if (closing) return; viewer.Navigate(-1); await Task.Delay(650); }
            if (closing) return;
            CloseViewerCore();
        }
        exerciseComplete = true; profile.Log("整合操作診斷完成");
    }
    async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return; e.Cancel = true; if (closing) return; closing = true;
        Model.Stop(); viewer.Close(); batchCancellation?.Cancel(); toastTimer.Stop();
        if (batchTask is not null) await batchTask;
        await images.DisposeAsync(); await workers.DisposeAsync(); await Model.FlushSettingsAsync();
        try
        {
            if (options.Metrics is string path)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); var process = Process.GetCurrentProcess();
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    frontEnd = "wpf",
                    buildProfile = App.BuildProfile,
                    libraryMilliseconds = libraryMs,
                    searchMilliseconds = searchMs,
                    itemCount = Model.Count,
                    maxRealizedContainers,
                    viewerSharpPaintSamplesMilliseconds = sharpSamples,
                    viewerSharpPaintCount = sharpSamples.Count,
                    viewerSharpTargetMisses = sharpSamples.Count(x => x > 500),
                    viewerNavigationUnpaintedSelections = unpainted,
                    viewerSharpTargetMilliseconds = 500,
                    exerciseComplete,
                    peakWorkingSetBytes = process.PeakWorkingSet64,
                    averageCpuUtilizationPercent = process.TotalProcessorTime.TotalMilliseconds / lifetime.Elapsed.TotalMilliseconds * 100,
                    cpuNormalizedByLogicalProcessors = false,
                    width = ActualWidth,
                    height = ActualHeight,
                    dpi = VisualTreeHelper.GetDpi(this).DpiScaleX
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex) { profile.Log("量測輸出失敗", ex); }
        profile.Log("正常關閉"); closed = true; Close();
    }
}
