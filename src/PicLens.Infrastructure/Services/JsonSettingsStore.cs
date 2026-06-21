using System.Text.Json;
using PicLens.Core.Services;
using PicLens.Core.Domain;
using PicLens.Core.Models;

namespace PicLens.Infrastructure.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string settingsPath;

    public JsonSettingsStore()
        : this(DefaultSettingsPath())
    {
    }

    public JsonSettingsStore(string settingsPath)
    {
        this.settingsPath = settingsPath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await LoadWithRecoveryAsync(cancellationToken);
        return result.Settings;
    }

    private async Task<SettingsLoadResult> LoadWithRecoveryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new SettingsLoadResult(AppSettings.CreateDefault(), ReadFailed: false, Quarantined: false);
        }

        try
        {
            await using var stream = File.OpenRead(settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            var normalized = settings is null
                ? AppSettings.CreateDefault()
                : SettingsRules.NormalizeSettings(settings);
            return new SettingsLoadResult(normalized, ReadFailed: false, Quarantined: false);
        }
        catch (Exception exception) when (IsExpectedReadFailure(exception))
        {
            var quarantined = QuarantineSettingsFile();
            return new SettingsLoadResult(AppSettings.CreateDefault(), ReadFailed: true, quarantined);
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = CreateTempPath(settingsPath);
        var replaced = false;
        var normalized = SettingsRules.NormalizeSettings(settings);

        try
        {
            await using (var stream = File.Open(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(settingsPath))
            {
                File.Replace(tempPath, settingsPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, settingsPath);
            }

            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                TryDeleteFile(tempPath);
            }
        }
    }

    public async Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadWithRecoveryAsync(cancellationToken);
        if (loadResult.ReadFailed && !loadResult.Quarantined)
        {
            throw new IOException("Settings update skipped because the existing settings file could not be read or quarantined.");
        }

        var updated = SettingsRules.MergeSettingsPatch(loadResult.Settings, patch);
        await SaveAsync(updated, cancellationToken);
        return updated;
    }

    private static string DefaultSettingsPath()
        => AppDataPaths.SettingsPath();

    private static bool IsExpectedReadFailure(Exception exception) =>
        exception is JsonException or IOException or UnauthorizedAccessException;

    private bool QuarantineSettingsFile()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                File.Move(settingsPath, CreateQuarantinePath(settingsPath));
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string CreateQuarantinePath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $"{Path.GetFileName(path)}.corrupt.{Guid.NewGuid():N}");
    }

    private static string CreateTempPath(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record SettingsLoadResult(AppSettings Settings, bool ReadFailed, bool Quarantined);
}
