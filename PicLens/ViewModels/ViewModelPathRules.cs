using PicLens.Diagnostics;

namespace PicLens.ViewModels;

internal static class ViewModelPathRules
{
    public static string FolderDisplayName(string path) =>
        FolderDisplayName(path, path);

    public static string FolderDisplayName(string? path, string fallback, IAppLogger? appLogger = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return fallback;
        }

        try
        {
            var normalized = Path.GetFullPath(path);
            var trimmed = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(name) ? normalized : name;
        }
        catch (Exception ex)
        {
            appLogger?.Error(ex, $"Folder segment lookup failed. Path={path}; Fallback={fallback}");
            return path;
        }
    }

    public static string ParentFolderDisplayName(string? path, IAppLogger appLogger)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "資料夾";
        }

        try
        {
            var parent = Directory.GetParent(Path.GetFullPath(path));
            return FolderDisplayName(parent?.FullName, "資料夾", appLogger);
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, $"Parent folder name lookup failed. Path={path}");
            return "資料夾";
        }
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
