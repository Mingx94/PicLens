using PicLens.Diagnostics;
using PicLens.Infrastructure.Services;

namespace PicLens;

public static class AppDataMigration
{
    private const string CurrentAppFolderName = "PicLens";
    private const string LegacyAppFolderName = "ImageViewerWin";
    private const string CurrentSettingsFileName = "piclens-settings.json";
    private const string LegacySettingsFileName = "image-viewer-settings.json";
    private const string CurrentLogFileName = "PicLens.log";
    private const string LegacyLogFileName = "ImageViewerWin.log";

    public static void MigrateLegacyData(IAppLogger logger, string? localAppDataRoot = null)
    {
        try
        {
            var (currentRoot, legacyRoot) = ResolveMigrationRoots(localAppDataRoot);

            if (!Directory.Exists(legacyRoot))
            {
                return;
            }

            CopyFileIfMissing(
                Path.Combine(legacyRoot, LegacySettingsFileName),
                Path.Combine(currentRoot, CurrentSettingsFileName));
            CopyDirectoryIfMissing(
                Path.Combine(legacyRoot, "Thumbnails"),
                Path.Combine(currentRoot, "Thumbnails"));
            CopyFileIfMissing(
                Path.Combine(legacyRoot, "Logs", LegacyLogFileName),
                Path.Combine(currentRoot, "Logs", CurrentLogFileName));
        }
        catch (Exception exception) when (IsExpectedMigrationFailure(exception))
        {
            logger.Error(exception, "Legacy app data migration failed.");
        }
    }

    private static (string CurrentRoot, string LegacyRoot) ResolveMigrationRoots(string? localAppDataRoot)
    {
        if (!string.IsNullOrWhiteSpace(localAppDataRoot))
        {
            return (
                Path.Combine(localAppDataRoot, CurrentAppFolderName),
                Path.Combine(localAppDataRoot, LegacyAppFolderName));
        }

        if (AppDataPaths.IsDataRootOverrideEnabled())
        {
            var currentRoot = AppDataPaths.AppRoot();
            var parent = Path.GetDirectoryName(currentRoot);
            return (
                currentRoot,
                Path.Combine(string.IsNullOrWhiteSpace(parent) ? currentRoot : parent, LegacyAppFolderName));
        }

        var root = ResolveLocalAppDataRoot(localAppDataRoot);
        return (
            Path.Combine(root, CurrentAppFolderName),
            Path.Combine(root, LegacyAppFolderName));
    }

    private static string ResolveLocalAppDataRoot(string? localAppDataRoot)
    {
        if (!string.IsNullOrWhiteSpace(localAppDataRoot))
        {
            return localAppDataRoot;
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(root) ? Path.GetTempPath() : root;
    }

    private static void CopyFileIfMissing(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath) || File.Exists(destinationPath))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
    }

    private static void CopyDirectoryIfMissing(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory) || Directory.Exists(destinationDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        Directory.CreateDirectory(destinationDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            var destinationFileDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationFileDirectory))
            {
                Directory.CreateDirectory(destinationFileDirectory);
            }

            File.Copy(file, destinationPath, overwrite: false);
        }
    }

    private static bool IsExpectedMigrationFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
