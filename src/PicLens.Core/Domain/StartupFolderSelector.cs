using PicLens.Core.Models;

namespace PicLens.Core.Domain;

public static class StartupFolderSelector
{
    public static string? SelectInitialFolder(string? lastFolderPath, Func<string, bool> isAvailable)
    {
        if (string.IsNullOrWhiteSpace(lastFolderPath))
        {
            return null;
        }

        return isAvailable(lastFolderPath) ? lastFolderPath : null;
    }
}
