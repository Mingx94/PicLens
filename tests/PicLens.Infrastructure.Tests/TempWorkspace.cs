namespace PicLens.Infrastructure.Tests;

internal sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public static TempWorkspace Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "PicLensTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new TempWorkspace(root);
    }

    public async Task<string> WriteFileAsync(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
