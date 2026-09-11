using System.Diagnostics;
using PicLens.Core;
using PicLens.Services;
using PicLens.App.Controls;
namespace PicLens.App;

public sealed class ViewerController(ImageService images, Profile profile, ViewerCanvas canvas)
{
    sealed record Preview(CancellationTokenSource Cancellation, Task<PreparedImage> Task);
    readonly Dictionary<string, Preview> previews = new(StringComparer.OrdinalIgnoreCase);
    CancellationTokenSource? original;
    List<LibraryEntry> sequence = []; int index, request;
    Stopwatch? clock; bool painted;
    public bool IsOpen { get; private set; }
    public string? CurrentPath => IsOpen && sequence.Count > 0 ? sequence[index].Path : null;
    public event Action<string, string>? StateChanged;
    public event Action<double>? SharpPaint;
    public event Action? Unpainted;
    public void Open(List<LibraryEntry> entries, string path)
    {
        Close(); sequence = [.. entries]; index = Math.Max(0, sequence.FindIndex(x => StringComparer.OrdinalIgnoreCase.Equals(x.Path, path)));
        IsOpen = sequence.Count > 0; canvas.Painted -= OnPainted; canvas.Painted += OnPainted;
        if (IsOpen) _ = Load();
    }
    public void Navigate(int delta)
    {
        if (!IsOpen) return; int next = Math.Clamp(index + delta, 0, sequence.Count - 1);
        if (next == index) return; RecordUnpainted(); index = next; _ = Load();
    }
    public void Close()
    {
        RecordUnpainted(); IsOpen = false; request++; original?.Cancel(); original = null;
        foreach (var p in previews.Values) Release(p); previews.Clear();
        canvas.SetImage(null, false, true);
    }
    void RecordUnpainted() { if (IsOpen && clock is not null && !painted && !sequence[index].Animated) Unpainted?.Invoke(); clock = null; }
    static void Release(Preview preview)
    {
        preview.Cancellation.Cancel();
        _ = preview.Task.ContinueWith(_ => preview.Cancellation.Dispose(), TaskScheduler.Default);
    }
    void OnPainted()
    {
        if (IsOpen && clock is not null && !painted) { painted = true; SharpPaint?.Invoke(clock.Elapsed.TotalMilliseconds); }
    }
    async Task<PreparedImage> Decode(string path, int edge, CancellationToken ct) =>
        await Task.Run(async () => PreparedImage.Create(await images.LoadAsync(path, edge, ct)), ct);
    Preview GetPreview(LibraryEntry entry)
    {
        if (previews.TryGetValue(entry.Path, out var existing)) return existing;
        var ct = new CancellationTokenSource(); var result = new Preview(ct, Decode(entry.Path, 1024, ct.Token)); previews.Add(entry.Path, result);
        _ = result.Task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        return result;
    }
    async Task Load()
    {
        int id = ++request; original?.Cancel(); var ct = original = new();
        clock = Stopwatch.StartNew(); painted = false;
        var entry = sequence[index]; canvas.SetImage(null, false, true);
        var keep = sequence.Skip(Math.Max(0, index - 1)).Take(index == 0 ? 2 : 3).Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in previews.Keys.Where(k => !keep.Contains(k)).ToArray()) { Release(previews[key]); previews.Remove(key); }
        StateChanged?.Invoke(entry.Name, $"{index + 1} / {sequence.Count} · 載入原圖…");
        if (entry.Animated) { StateChanged?.Invoke(entry.Name, "動畫圖片目前不支援預覽"); clock = null; ct.Dispose(); original = null; return; }
        try
        {
            try
            {
                var preview = await GetPreview(entry).Task.WaitAsync(ct.Token);
                if (id != request || !IsOpen) return; canvas.SetImage(preview, false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { profile.Log("Viewer 預覽失敗 " + entry.Path, ex); }
            var full = await Decode(entry.Path, 0, ct.Token);
            if (id != request || !IsOpen) return;
            canvas.SetImage(full, true);
            StateChanged?.Invoke(entry.Name, $"{index + 1} / {sequence.Count} · {full.Width:N0} × {full.Height:N0}");
            foreach (int adjacent in new[] { index + 1, index - 1 })
            {
                if (id != request || !IsOpen) break;
                if (adjacent >= 0 && adjacent < sequence.Count && !sequence[adjacent].Animated)
                {
                    try { await GetPreview(sequence[adjacent]).Task.WaitAsync(ct.Token); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { profile.Log("預載失敗", ex); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (id == request && IsOpen) StateChanged?.Invoke(entry.Name, "無法載入原圖：" + ex.Message);
            profile.Log("Viewer 原圖失敗 " + entry.Path, ex);
        }
        finally { ct.Dispose(); if (ReferenceEquals(original, ct)) original = null; }
    }
}
