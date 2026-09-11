using System.Collections.ObjectModel;
using System.Diagnostics;
using PicLens.Core;
using PicLens.Services;
namespace PicLens.App;

public sealed class LibraryViewModel : Observable
{
    readonly Profile profile; readonly Scanner scanner;
    public Settings Settings { get; private set; }
    public Selection Selection { get; } = new();
    public FolderHistory History { get; } = new();
    public ResetCollection<TileModel> Items { get; } = new();
    public ObservableCollection<FolderNode> Roots { get; } = [];
    public ThumbnailController Thumbnails { get; }
    public List<LibraryEntry> Source { get; private set; } = [];
    public event Action<double>? LibraryMeasured;
    public event Action<double>? SearchMeasured;
    CancellationTokenSource? scan; CancellationTokenSource tree = new(); int generation;
    Task saveTask = Task.CompletedTask;
    int saveRequest;
    string? settingsSaveError;
    public string? SettingsSaveError { get => settingsSaveError; private set => Set(ref settingsSaveError, value); }
    string folder = "", root = "", search = "", status = "選擇資料夾，開始瀏覽圖片。"; bool loading;
    public string Folder { get => folder; private set { Set(ref folder, value); Changed(nameof(FolderName)); } }
    public string FolderName => string.IsNullOrEmpty(Folder) ? "你的圖片，一目了然。" : System.IO.Path.GetFileName(Folder.TrimEnd('\\'));
    public string RootPath { get => root; private set => Set(ref root, value); }
    public string Search { get => search; set { if (Set(ref search, value)) Project(); } }
    public string Status { get => status; set => Set(ref status, value); }
    public bool Loading { get => loading; private set => Set(ref loading, value); }
    public int Count => Items.Count;
    public string SelectionText => Selection.Ordered.Count == 0 ? "未選取圖片" : $"已選取 {Selection.Ordered.Count} 張";
    public double TileWidth => Settings.ThumbnailSize + 24;
    public double TileHeight => Settings.ThumbnailSize + 70;
    public int ThumbnailSize
    {
        get => Settings.ThumbnailSize;
        set { Settings = (Settings with { ThumbnailSize = value }).Normalize(); Changed(); Changed(nameof(TileWidth)); Changed(nameof(TileHeight)); Save(); }
    }
    public bool Recursive
    {
        get => Settings.IncludeSubfolders;
        set { if (Settings.IncludeSubfolders == value) return; Settings = Settings with { IncludeSubfolders = value }; Changed(); Save(); _ = Navigate(Folder, false); }
    }
    public bool SidebarCollapsed
    {
        get => Settings.SidebarCollapsed;
        set { Settings = Settings with { SidebarCollapsed = value }; Changed(); Save(); }
    }
    public int SortIndex
    {
        get => Settings.Sort.Key * 2 + Settings.Sort.Direction;
        set { Settings = Settings with { Sort = new() { Key = value / 2, Direction = value % 2 } }; Changed(); Save(); Source = LibraryRules.Sort(Source, Settings.Sort, !Recursive); Project(); }
    }
    public LibraryViewModel(Profile profile, ImageService images)
    {
        this.profile = profile; scanner = new(profile); Settings = profile.Load(); Thumbnails = new(images, profile);
    }
    void Save()
    {
        var snapshot = Settings;
        saveTask = SaveAfterAsync(saveTask, snapshot, ++saveRequest);
    }
    async Task SaveAfterAsync(Task previous, Settings snapshot, int request)
    {
        await previous;
        try {
            await Task.Run(() => profile.Save(snapshot));
            if (request == saveRequest) SettingsSaveError = null;
        } catch (Exception ex) {
            profile.Log("設定寫入失敗", ex);
            if (request == saveRequest) SettingsSaveError = "設定尚未儲存，重開後可能失去這次變更。請確認資料目錄可寫入後重試。";
        }
    }
    public Task RetrySettingsSaveAsync() { Save(); return saveTask; }
    public Task FlushSettingsAsync() => saveTask;
    public async Task Pick(string path, bool persist = true)
    {
        if (persist) { Settings = Settings with { LastFolderPath = path }; Save(); }
        tree.Cancel(); tree.Dispose(); tree = new(); var token = tree.Token; RootPath = path; Roots.Clear();
        try
        {
            var children = await scanner.ChildrenAsync(path, token);
            token.ThrowIfCancellationRequested();
            foreach (var child in children) Roots.Add(NewNode(child));
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Status = ex.Message; profile.Log("資料夾樹載入失敗", ex); }
        await Navigate(path);
    }
    static FolderNode NewNode(string path) { var node = new FolderNode(path); node.Children.Add(new("")); return node; }
    public async Task Expand(FolderNode node)
    {
        if (node.Loaded || string.IsNullOrEmpty(node.Path)) return;
        var token = tree.Token;
        try
        {
            var children = await scanner.ChildrenAsync(node.Path, token); token.ThrowIfCancellationRequested();
            node.Children.Clear(); foreach (var child in children) node.Children.Add(NewNode(child)); node.Loaded = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { Status = "無法展開：" + ex.Message; profile.Log("樹展開失敗", ex); }
        catch (OperationCanceledException) { }
    }
    public async Task Navigate(string path, bool history = true)
    {
        if (string.IsNullOrEmpty(path)) return;
        scan?.Cancel(); var tokenSource = scan = new(); int id = ++generation;
        Folder = path; if (history) History.Visit(path); Loading = true; Status = "正在讀取資料夾…";
        Source = []; Thumbnails.Reset(); Selection.Clear(); Items.Reset([]); Changed(nameof(Count)); Changed(nameof(SelectionText));
        var timer = Stopwatch.StartNew();
        try
        {
            var result = await scanner.ScanAsync(path, Recursive, tokenSource.Token);
            var sorted = await Task.Run(() => LibraryRules.Sort(result, Settings.Sort, !Recursive), tokenSource.Token);
            if (id != generation) return;
            Source = sorted; Project(); Status = $"{Items.Count:N0} 個項目"; LibraryMeasured?.Invoke(timer.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (id == generation) Status = "無法讀取資料夾：" + ex.Message; profile.Log("導覽失敗 " + path, ex); }
        finally { if (id == generation) Loading = false; tokenSource.Dispose(); if (ReferenceEquals(scan, tokenSource)) scan = null; }
    }
    public void Project()
    {
        var timer = Stopwatch.StartNew(); Thumbnails.Reset(); Selection.Clear();
        Items.Reset(LibraryRules.Search(Source, Search).Select(e => new TileModel(e)));
        Changed(nameof(Count)); Changed(nameof(SelectionText)); SearchMeasured?.Invoke(timer.Elapsed.TotalMilliseconds);
    }
    public void Select(TileModel tile, bool ctrl, bool shift)
    {
        if (tile.IsFolder) return;
        Selection.Select(tile.Path, Items.Where(x => !x.IsFolder).Select(x => x.Path).ToList(), ctrl, shift); SyncSelection();
    }
    public bool SyncingSelection { get; private set; }
    public void SyncSelection()
    {
        SyncingSelection = true;
        try { foreach (var item in Items) item.Selected = Selection.Contains(item.Path); Changed(nameof(SelectionText)); }
        finally { SyncingSelection = false; }
    }
    public void Stop() { generation++; scan?.Cancel(); tree.Cancel(); Thumbnails.Reset(); }
}
