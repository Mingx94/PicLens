using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
namespace PicLens.Services;

public sealed class WorkerPool : IAsyncDisposable
{
    sealed record Job(string[] Arguments, CancellationToken Token, TimeSpan Timeout, TaskCompletionSource<bool> Completion);
    readonly Channel<Job> queue = Channel.CreateBounded<Job>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
    readonly CancellationTokenSource shutdown = new();
    readonly Task[] pumps;
    readonly string executable;
    readonly Profile profile;
    int active;
    public int ActiveCount => Volatile.Read(ref active);
    public WorkerPool(Profile profile, string? executable = null)
    {
        this.profile = profile;
        this.executable = executable ?? Path.Combine(AppContext.BaseDirectory, "worker", "PicLens.Worker.exe");
        pumps = Enumerable.Range(0, 8).Select(_ => Task.Run(Pump)).ToArray();
    }
    public async Task RunAsync(string[] arguments, CancellationToken ct, TimeSpan? timeout = null)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, shutdown.Token);
        await queue.Writer.WriteAsync(new(arguments, ct, timeout ?? TimeSpan.FromSeconds(15), completion), linked.Token);
        await completion.Task;
    }
    async Task Pump()
    {
        await foreach (var job in queue.Reader.ReadAllAsync())
        {
            if (job.Token.IsCancellationRequested || shutdown.IsCancellationRequested) { job.Completion.TrySetCanceled(); continue; }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.Token, shutdown.Token);
            linked.CancelAfter(job.Timeout);
            using var process = new Process();
            process.StartInfo = new(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in job.Arguments) process.StartInfo.ArgumentList.Add(arg);
            bool started = false;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                process.Start(); started = true; Interlocked.Increment(ref active);
                var errors = process.StandardError.ReadToEndAsync();
                var output = process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(linked.Token);
                string message = await errors; await output;
                if (process.ExitCode != 0) throw new IOException(string.IsNullOrWhiteSpace(message) ? "背景圖片工作失敗。" : message.Trim());
                job.Completion.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                if (started) { try { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); } catch (InvalidOperationException) { } }
                if (!job.Token.IsCancellationRequested && !shutdown.IsCancellationRequested)
                    job.Completion.TrySetException(new TimeoutException("背景工作逾時，已終止並回收。"));
                else job.Completion.TrySetCanceled();
            }
            catch (Exception ex) { profile.Log("背景工作失敗", ex); job.Completion.TrySetException(ex); }
            finally { if (started) Interlocked.Decrement(ref active); }
        }
    }
    public async ValueTask DisposeAsync()
    {
        shutdown.Cancel(); queue.Writer.TryComplete(); await Task.WhenAll(pumps); shutdown.Dispose();
    }
}
public sealed record Pixels(int Width, int Height, int Stride, byte[] Bytes);
public sealed class ImageService(Profile profile, WorkerPool workers) : IAsyncDisposable
{
    readonly CancellationTokenSource stop = new();
    int dirty;
    Task? cleaning;
    public void StartCleanup() => cleaning ??= Task.Run(CleanupLoop);
    public async Task<Pixels> LoadAsync(string source, int edge, CancellationToken ct)
    {
        var info = new FileInfo(source);
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{Path.GetFullPath(source)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{edge}|bgra-v1")));
        string png = Path.Combine(profile.Cache, key + ".png");
        string temp = Path.Combine(profile.Temporary, Guid.NewGuid().ToString("N") + ".pixels");
        // A bounded worker queue owns decoder concurrency; callers own only their visible requests.
        try
        {
            bool persistent = edge > 0 && edge != 1024;
            string? cacheTemp = persistent ? Path.Combine(profile.Cache, key + "." + Guid.NewGuid().ToString("N") + ".tmp") : null;
            try
            {
                bool warm = persistent && File.Exists(png);
                try
                {
                    await workers.RunAsync(warm ? ["decode", png, temp, "0"] :
                        cacheTemp is not null ? ["decode", source, temp, edge.ToString(), cacheTemp] : ["decode", source, temp, edge.ToString()], ct);
                }
                catch (IOException ex) when (warm)
                {
                    profile.Log("重建損壞的縮圖快取", ex); TryDelete(png);
                    await workers.RunAsync(["decode", source, temp, edge.ToString(), cacheTemp!], ct);
                }
                ct.ThrowIfCancellationRequested();
                var after = new FileInfo(source);
                if (after.Length != info.Length || after.LastWriteTimeUtc != info.LastWriteTimeUtc) throw new IOException("圖片在載入期間已變更。");
                if (cacheTemp is not null && File.Exists(cacheTemp)) { File.Move(cacheTemp, png, true); Interlocked.Exchange(ref dirty, 1); }
                return await Task.Run(() => ReadPixels(temp), ct);
            }
            finally { if (cacheTemp is not null) TryDelete(cacheTemp); }
        }
        finally { TryDelete(temp); }
    }
    public static Pixels ReadPixels(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.ReadInt32() != 0x504C5058) throw new IOException("圖片資料標頭不正確。");
        int width = reader.ReadInt32(), height = reader.ReadInt32(), stride = reader.ReadInt32();
        if (!OriginalImageLimits.FitsDimensions(width, height) || stride != (long)width * 4)
            throw new IOException("圖片資料超出允許範圍。");
        long byteCount = (long)stride * height;
        if (reader.BaseStream.Length - reader.BaseStream.Position < byteCount) throw new IOException("圖片資料不完整。");
        byte[] bytes = reader.ReadBytes(checked((int)byteCount));
        if (bytes.Length != byteCount) throw new IOException("圖片資料不完整。");
        return new(width, height, stride, bytes);
    }
    async Task CleanupLoop()
    {
        try
        {
            Prune();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stop.Token)) if (Interlocked.Exchange(ref dirty, 0) != 0) Prune();
        }
        catch (OperationCanceledException) { }
    }
    void Prune()
    {
        try { foreach (var f in new DirectoryInfo(profile.Cache).EnumerateFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).Skip(2000)) f.Delete(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { profile.Log("快取清理失敗", ex); }
    }
    static void TryDelete(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    public async ValueTask DisposeAsync() { stop.Cancel(); if (cleaning is not null) await cleaning; stop.Dispose(); }
}
