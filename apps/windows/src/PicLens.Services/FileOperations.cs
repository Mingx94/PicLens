using System.Diagnostics;
using PicLens.Core;
namespace PicLens.Services;

public sealed class FileOperations(Profile profile, WorkerPool workers)
{
    public async Task<BatchResult> ExecuteAsync(IReadOnlyList<FilePlan> plans, IProgress<string>? progress, CancellationToken ct)
    {
        var results = new List<FileResult>();
        foreach (var plan in plans)
        {
            if (ct.IsCancellationRequested) { results.Add(new(plan.Source, plan.Target, ResultStatus.Canceled, "尚未執行")); continue; }
            if (plan.Skip is not null) { results.Add(new(plan.Source, plan.Target, ResultStatus.Skipped, plan.Skip)); continue; }
            progress?.Report($"正在處理 {results.Count + 1} / {plans.Count} · {Path.GetFileName(plan.Source)}");
            try
            {
                results.Add(await Task.Run(async () =>
                {
                    if (!File.Exists(plan.Source)) throw new IOException("來源檔案已不存在。");
                    if (HasReparsePoint(plan.Source) || (plan.Target is not null && HasReparsePoint(Path.GetDirectoryName(plan.Target)!)))
                        throw new IOException("不修改符號連結或 junction 內的檔案。");
                    if (FileStamp.Read(plan.Source) != plan.Stamp) throw new IOException("來源檔案已變更，請重新確認。");
                    if (plan.Target is not null && StringComparer.OrdinalIgnoreCase.Equals(plan.Source, plan.Target))
                        return new FileResult(plan.Source, plan.Target, ResultStatus.Skipped, "檔名未變更");
                    if (plan.Target is not null && (File.Exists(plan.Target) || Directory.Exists(plan.Target)))
                        return new FileResult(plan.Source, plan.Target, ResultStatus.Skipped, "目標已存在，未覆寫");
                    if (plan.CheckStem && Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(plan.Target!)!).Any(p =>
                        !StringComparer.OrdinalIgnoreCase.Equals(p, plan.Source) &&
                        StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(p), Path.GetFileNameWithoutExtension(plan.Target))))
                        return new FileResult(plan.Source, plan.Target, ResultStatus.Skipped, "目標序號已占用");
                    ct.ThrowIfCancellationRequested();
                    if (plan.Kind == OperationKind.Rename) File.Move(plan.Source, plan.Target!, false);
                    else if (plan.Kind == OperationKind.Trash) await workers.RunAsync(["trash", plan.Source], ct);
                    else
                    {
                        string temporary = Path.Combine(Path.GetDirectoryName(plan.Target!)!, ".piclens-" + Guid.NewGuid().ToString("N") + ".tmp");
                        try
                        {
                            await workers.RunAsync(["convert", plan.Source, temporary, plan.Kind == OperationKind.Jpeg ? "jpg" : "webp"], ct);
                            ct.ThrowIfCancellationRequested();
                            if (FileStamp.Read(plan.Source) != plan.Stamp) throw new IOException("轉換期間來源已變更，未提交輸出。");
                            File.Move(temporary, plan.Target!, false);
                        }
                        finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    }
                    return new FileResult(plan.Source, plan.Target, ResultStatus.Succeeded, "完成");
                }, ct));
            }
            catch (OperationCanceledException)
            {
                results.Add(new(plan.Source, plan.Target, plan.Kind == OperationKind.Trash ? ResultStatus.Unknown : ResultStatus.Canceled,
                    plan.Kind == OperationKind.Trash ? "回收程序已取消，請確認來源是否仍存在。" : "已取消"));
            }
            catch (Exception ex)
            {
                profile.Log($"檔案操作失敗 source={plan.Source} target={plan.Target} kind={plan.Kind}", ex);
                results.Add(new(plan.Source, plan.Target, plan.Kind == OperationKind.Trash && ex is TimeoutException ? ResultStatus.Unknown : ResultStatus.Failed, ex.Message));
            }
        }
        foreach (var result in results) profile.Log($"檔案結果 status={result.Status} source={result.Source} target={result.Target} reason={result.Message}");
        var batch = new BatchResult(results); profile.Log("檔案操作完成 " + batch.Summary); return batch;
    }
    public static void Reveal(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("檔案已不存在。", path);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "/select,\"" + Path.GetFullPath(path) + "\"" });
    }
    static bool HasReparsePoint(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            current = Path.GetDirectoryName(current);
        }
        return false;
    }
}
