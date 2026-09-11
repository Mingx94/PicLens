using System.Text.Json;
using PicLens.Core;
namespace PicLens.Services;

public sealed class Profile
{
    public string Root { get; }
    public string Cache => Path.Combine(Root, "Thumbnails", "wpf-v1");
    public string Temporary => Path.Combine(Root, "Temporary");
    public string SettingsPath => Path.Combine(Root, "piclens-settings.json");
    public string LogPath => Path.Combine(Root, "Logs", "PicLens.log");
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    readonly object gate = new(); bool unsafeToWrite;
    public Profile(string? overrideRoot = null)
    {
        string? configured = overrideRoot ?? Environment.GetEnvironmentVariable("PICLENS_DATA_ROOT");
        Root = string.IsNullOrWhiteSpace(configured) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PicLens") :
            Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured.Trim()));
        Directory.CreateDirectory(Cache); Directory.CreateDirectory(Temporary); Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
    }
    public Settings Load()
    {
        lock (gate)
        {
            if (!File.Exists(SettingsPath)) return new();
            try { return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath), Json) ?? throw new JsonException("設定為 null")).Normalize(); }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Log("設定讀取失敗", ex);
                try { File.Move(SettingsPath, SettingsPath + ".corrupt." + Guid.NewGuid().ToString("N")); }
                catch (Exception quarantine) { unsafeToWrite = true; Log("設定隔離失敗；禁止覆寫", quarantine); }
                return new();
            }
        }
    }
    public void Save(Settings settings)
    {
        lock (gate)
        {
            if (unsafeToWrite) throw new IOException("舊設定無法隔離，已停止寫入以保留資料。");
            string tmp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, settings.Normalize(), Json); stream.Flush(true);
                }
                if (File.Exists(SettingsPath)) File.Replace(tmp, SettingsPath, null);
                else File.Move(tmp, SettingsPath, false);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
    public void Log(string message, Exception? error = null)
    {
        lock (gate)
        {
            try { File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [WPF] {message} {error}\n"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
