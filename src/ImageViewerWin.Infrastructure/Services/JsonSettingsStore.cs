using System.Text.Json;
using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Domain;
using ImageViewerWin.Core.Models;

namespace ImageViewerWin.Infrastructure.Services;

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
        if (!File.Exists(settingsPath))
        {
            return AppSettings.CreateDefault();
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
        return settings is null
            ? AppSettings.CreateDefault()
            : SettingsRules.NormalizeSettings(settings);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        await using var stream = File.Create(settingsPath);
        var normalized = SettingsRules.NormalizeSettings(settings);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
    }

    public async Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default)
    {
        var current = await LoadAsync(cancellationToken);
        var updated = SettingsRules.MergeSettingsPatch(current, patch);
        await SaveAsync(updated, cancellationToken);
        return updated;
    }

    private static string DefaultSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "ImageViewerWin", "image-viewer-settings.json");
    }
}
