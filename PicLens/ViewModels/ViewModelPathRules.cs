namespace PicLens.ViewModels;

internal static class ViewModelPathRules
{
    public static string FolderDisplayName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    public static Func<string, string, bool> CreateTargetNameExists(string targetPath)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("目標路徑必須包含資料夾。");
        var existingPaths = Directory.Exists(targetDirectory)
            ? Directory.EnumerateFiles(targetDirectory).ToList()
            : new List<string>();

        return (candidatePath, sourcePath) =>
            PicLens.Core.Domain.PathRules.TargetNameExists(existingPaths, candidatePath, sourcePath);
    }

    public static bool IsPathAncestorOrEqual(string ancestorPath, string childPath)
    {
        var ancestor = Path.GetFullPath(ancestorPath);
        var child = Path.GetFullPath(childPath);
        if (PicLens.Core.Domain.PathRules.PathEquals(ancestor, child))
        {
            return true;
        }

        var relative = Path.GetRelativePath(ancestor, child);
        return relative != "."
            && !relative.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    public static bool IsPathAncestor(string ancestorPath, string childPath) =>
        !PicLens.Core.Domain.PathRules.PathEquals(ancestorPath, childPath)
        && IsPathAncestorOrEqual(ancestorPath, childPath);
}
