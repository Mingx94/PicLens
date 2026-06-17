using PicLens.Core.Domain;
using PicLens.Core.Models;
using static PicLens.ViewModels.ViewModelPathRules;
using static PicLens.Core.Domain.PathRules;

namespace PicLens.ViewModels;

public sealed partial class MainPageViewModel
{
    private const int MaxConcurrentThumbnailLoads = 4;
    private static readonly TimeSpan DefaultThumbnailLoadTimeout = TimeSpan.FromSeconds(8);

    private readonly TimeSpan thumbnailLoadTimeout;
    private readonly SemaphoreSlim thumbnailGate = new(MaxConcurrentThumbnailLoads);
    private readonly Dictionary<LibraryTileItem, ThumbnailLoadState> thumbnailLoads = new(ReferenceEqualityComparer.Instance);

    public async Task ChangeThumbnailSizeAsync(double thumbnailSize)
    {
        var normalizedSize = SettingsRules.NormalizeThumbnailSize(thumbnailSize);
        if (ThumbnailSize == normalizedSize)
        {
            return;
        }

        ThumbnailSize = normalizedSize;
        CancelAllThumbnailLoads();
        ApplyThumbnailSizeToLibraryItems();
        settings = await settingsStore.UpdateAsync(new AppSettingsPatch { ThumbnailSize = normalizedSize });
        SetStatus($"縮圖大小已調整為 {normalizedSize}。");
    }

    public async Task LoadThumbnailAsync(LibraryTileItem tile)
    {
        if (tile.IsFolder || tile.IsAnimated || tile.SourceItem is not ImageListItem image)
        {
            return;
        }

        var requestedSize = ThumbnailSize;
        if (tile.HasThumbnailFor(requestedSize))
        {
            return;
        }

        if (thumbnailLoads.TryGetValue(tile, out var existingLoad)
            && existingLoad.RequestedSize == requestedSize
            && !existingLoad.CancellationSource.IsCancellationRequested)
        {
            return;
        }

        CancelThumbnailLoad(tile);

        var loadCts = new CancellationTokenSource();
        var loadState = new ThumbnailLoadState(loadCts, requestedSize);
        thumbnailLoads[tile] = loadState;

        try
        {
            await thumbnailGate.WaitAsync(loadCts.Token);
            try
            {
                using var timeoutCts = new CancellationTokenSource(thumbnailLoadTimeout);
                using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(loadCts.Token, timeoutCts.Token);
                var token = operationCts.Token;
                var thumbnailTask = thumbnailService.GetOrCreateThumbnailAsync(image.Path, requestedSize, token);
                var thumbnailPath = await WaitForThumbnailResultAsync(thumbnailTask, token);
                if (!loadCts.IsCancellationRequested
                    && ThumbnailSize == requestedSize
                    && LibraryItems.Contains(tile)
                    && PathEquals(tile.Path, image.Path))
                {
                    await ApplyThumbnailPathAsync(tile, image, thumbnailPath, requestedSize);
                }
            }
            finally
            {
                thumbnailGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, $"Load thumbnail failed. Image={image.Name}; Path={image.Path}; RequestedSize={requestedSize}");
        }
        finally
        {
            if (thumbnailLoads.TryGetValue(tile, out var activeLoad) && ReferenceEquals(activeLoad, loadState))
            {
                thumbnailLoads.Remove(tile);
            }

            loadCts.Dispose();
        }
    }

    private Task ApplyThumbnailPathAsync(
        LibraryTileItem tile,
        ImageListItem image,
        string? thumbnailPath,
        int requestedSize)
    {
        void Apply()
        {
            if (ThumbnailSize == requestedSize
                && LibraryItems.Contains(tile)
                && PathEquals(tile.Path, image.Path))
            {
                tile.ApplyThumbnailPath(thumbnailPath, requestedSize);
            }
        }

        if (dispatcherService.HasUiThreadAccess)
        {
            Apply();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherService.TryEnqueue(() =>
            {
                try
                {
                    Apply();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            }))
        {
            appLogger.Error(
                new InvalidOperationException("Failed to enqueue thumbnail UI update."),
                $"Queue thumbnail update failed. Image={image.Name}; Path={image.Path}; RequestedSize={requestedSize}");
            return Task.CompletedTask;
        }

        return completion.Task;
    }

    public void CancelThumbnailLoad(LibraryTileItem tile)
    {
        if (thumbnailLoads.TryGetValue(tile, out var loadState))
        {
            loadState.CancellationSource.Cancel();
        }
    }

    private void ApplyThumbnailSizeToLibraryItems()
    {
        foreach (var item in LibraryItems)
        {
            item.ApplyThumbnailSize(ThumbnailSize);
        }
    }

    private void CancelAllThumbnailLoads()
    {
        foreach (var loadState in thumbnailLoads.Values)
        {
            loadState.CancellationSource.Cancel();
        }

        thumbnailLoads.Clear();
    }

    private static async Task<string?> WaitForThumbnailResultAsync(
        Task<string?> thumbnailTask,
        CancellationToken cancellationToken)
    {
        try
        {
            return await thumbnailTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!thumbnailTask.IsCompleted)
        {
            ObserveFaultIfThumbnailTaskFailsLater(thumbnailTask);
            throw;
        }
    }

    private static void ObserveFaultIfThumbnailTaskFailsLater(Task thumbnailTask)
    {
        _ = thumbnailTask.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private sealed record ThumbnailLoadState(CancellationTokenSource CancellationSource, int RequestedSize);
}
