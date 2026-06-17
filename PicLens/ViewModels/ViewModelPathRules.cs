namespace PicLens.ViewModels;

internal static class ViewModelPathRules
{
    public static string FolderDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    public static bool PathEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static Func<string, string, bool> CreateTargetNameExists(string targetPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("目標路徑必須包含資料夾。");
        var existingPaths = Directory.Exists(targetDirectory)
            ? Directory.EnumerateFiles(targetDirectory).ToList()
            : new List<string>();

        return (candidatePath, sourcePath) => existingPaths.Any(path =>
            !PathEquals(path, sourcePath)
            && HasSameDirectoryAndBasenameWithoutExtension(path, candidatePath));
    }

    public static bool IsPathAncestorOrEqual(string ancestorPath, string childPath)
    {
        var ancestor = Path.GetFullPath(ancestorPath);
        var child = Path.GetFullPath(childPath);
        if (PathEquals(ancestor, child))
        {
            return true;
        }

        var relative = Path.GetRelativePath(ancestor, child);
        return relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    public static bool IsPathAncestor(string ancestorPath, string childPath) =>
        !PathEquals(ancestorPath, childPath)
        && IsPathAncestorOrEqual(ancestorPath, childPath);

    private static bool HasSameDirectoryAndBasenameWithoutExtension(string left, string right)
    {
        var leftDirectory = Path.GetDirectoryName(left);
        var rightDirectory = Path.GetDirectoryName(right);
        return leftDirectory is not null
            && rightDirectory is not null
            && PathEquals(leftDirectory, rightDirectory)
            && string.Equals(
                Path.GetFileNameWithoutExtension(left),
                Path.GetFileNameWithoutExtension(right),
                StringComparison.OrdinalIgnoreCase);
    }
}
