namespace PicLens.Core.Domain;

public static class PathRules
{
    public static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;

    public static string PathKey(string path) => Path.GetFullPath(path);

    public static bool PathEquals(string? left, string? right) =>
        left is not null
        && right is not null
        && PathComparer.Equals(PathKey(left), PathKey(right));

    public static bool HasSameDirectoryAndBasenameWithoutExtension(string left, string right) =>
        Path.GetDirectoryName(left) is { } leftDirectory
        && Path.GetDirectoryName(right) is { } rightDirectory
        && PathEquals(leftDirectory, rightDirectory)
        && string.Equals(
            Path.GetFileNameWithoutExtension(left),
            Path.GetFileNameWithoutExtension(right),
            StringComparison.OrdinalIgnoreCase);

    public static bool TargetNameExists(IEnumerable<string> existingPaths, string candidatePath, string sourcePath) =>
        existingPaths.Any(path =>
            !PathEquals(path, sourcePath)
            && HasSameDirectoryAndBasenameWithoutExtension(path, candidatePath));
}
