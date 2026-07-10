using CommunityToolkit.Mvvm.Input;
using PicLens.Core.Domain;
using PicLens.Core.Models;
using static PicLens.ViewModels.ViewModelPathRules;

namespace PicLens.ViewModels;

public sealed partial class MainPageViewModel
{
    public void BeginImageDrag(IEnumerable<LibraryTileItem> selectedItems)
    {
        var selectedImages = selectedItems.Select(item => item.SourceItem).OfType<ImageListItem>().ToList();
        dragSources.Clear();
        dragSources.AddRange(selectedImages);
        appLogger.Info(
            $"Begin image drag. SourceCount={dragSources.Count}; First={dragSources.FirstOrDefault()?.Name ?? "<none>"}");
    }

    public async Task DropDraggedImagesOnAsync(LibraryTileItem target)
    {
        if (target.SourceItem is not ImageListItem targetImage || dragSources.Count == 0)
        {
            appLogger.Info(
                $"Drop dragged images ignored. HasTargetImage={target.SourceItem is ImageListItem}; SourceCount={dragSources.Count}");
            return;
        }

        appLogger.Info(
            $"Drop dragged images started. SourceCount={dragSources.Count}; Target={targetImage.Name}; TargetPath={targetImage.Path}");

        try
        {
            var plan = FileRenamePlanner.PlanDropTargetBatchRename(
                dragSources.Select(image => image.Path),
                targetImage.Path,
                ExistingTargetDirectoryFiles(targetImage.Path));
            if (plan.Total == 0)
            {
                SetStatus("沒有可拖放重新命名的圖片。");
                appLogger.Info(
                    $"Drop dragged images ignored. Reason=EmptyPreview; Target={targetImage.Name}; TargetPath={targetImage.Path}");
                return;
            }

            if (!await dialogService.ConfirmDropRenameAsync(plan))
            {
                SetStatus("已取消拖放重新命名。");
                appLogger.Info(
                    $"Drop dragged images canceled. Total={plan.Total}; RenameCount={plan.Items.Count(item => !item.ShouldSkip)}; SkippedCount={plan.Items.Count(item => item.ShouldSkip)}; Target={targetImage.Name}");
                return;
            }

            var result = await fileOperationService.RenameByDropTargetAsync(dragSources.Select(image => image.Path), targetImage.Path);
            ClearSelection();
            SetBatchStatus("拖放重新命名", result);
            LogBatchItemFailures("Drop dragged images", result);
            appLogger.Info(
                $"Drop dragged images completed. Total={result.Total}; Succeeded={result.Succeeded}; Skipped={result.Skipped}; Failed={result.Failed}; Target={targetImage.Name}");
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Drop dragged images failed.");
            SetStatus("拖放重新命名時發生錯誤，已寫入診斷記錄。");
        }
        finally
        {
            dragSources.Clear();
        }
    }

    private void LogBatchItemFailures(string operationName, FileOperationBatchResult result)
    {
        foreach (var item in result.Items.Where(item => item.Status == FileOperationStatus.Failed))
        {
            var details = item.Message ?? item.Reason ?? "File operation failed.";
            appLogger.Error(
                new IOException(details),
                $"{operationName} item failed. Path={item.Path}; TargetPath={item.TargetPath ?? "<none>"}; Reason={item.Reason ?? "<none>"}");
        }
    }

    [RelayCommand(CanExecute = nameof(IsFileOperationActive))]
    private void CancelFileOperation()
    {
        fileOperationCancellationSource?.Cancel();
        SetStatus("正在取消目前檔案操作。");
    }

    [RelayCommand]
    private async Task ConvertVisible()
    {
        if (IsFileOperationActive)
        {
            return;
        }

        var images = VisibleImages();
        if (images.Count == 0)
        {
            SetStatus("沒有可轉換的圖片。");
            return;
        }

        if (!await ConfirmLargeBatchAsync(images.Count, "要將目前顯示的 {0} 張圖片轉為 JPG 嗎？", "轉換為 JPG", "開始轉換"))
        {
            return;
        }

        var operation = BeginFileOperation();
        SetStatus($"正在轉換 {images.Count} 張圖片為 JPG…");
        try
        {
            var result = await fileOperationService.ConvertVisibleToJpgAsync(images, operation.Token);
            SetBatchStatus("轉換為 JPG", result);
            await LoadLibraryAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消轉換為 JPG。");
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Convert visible images failed.");
            SetStatus("轉換為 JPG 時發生錯誤，已寫入診斷記錄。");
        }
        finally
        {
            EndFileOperation(operation);
        }
    }

    [RelayCommand]
    private async Task ClearSameBasename()
    {
        if (IsFileOperationActive)
        {
            return;
        }

        var images = VisibleImages();
        if (images.Count == 0)
        {
            SetStatus("沒有可清除的圖片。");
            return;
        }

        if (!await dialogService.ConfirmAsync($"要將目前顯示的 {images.Count} 張圖片中，同名的非 JPG 檔案移至回收筒嗎？", "清除同名檔案", "移至回收筒"))
        {
            return;
        }

        var operation = BeginFileOperation();
        SetStatus("正在清除同名的非 JPG 圖片…");
        try
        {
            var result = await fileOperationService.TrashSameBasenameNonJpgAsync(images, operation.Token);
            SetBatchStatus("清除同名檔案", result);
            await LoadLibraryAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("已取消清除同名檔案。");
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Clear same basename images failed.");
            SetStatus("清除同名檔案時發生錯誤，已寫入診斷記錄。");
        }
        finally
        {
            EndFileOperation(operation);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSingleSelectedImage))]
    private async Task RenameSelected()
    {
        try
        {
            var selected = SelectedImages().SingleOrDefault();
            if (selected is null)
            {
                return;
            }

            var nextName = await dialogService.RequestRenameAsync(selected);
            if (string.IsNullOrWhiteSpace(nextName))
            {
                return;
            }

            var result = await fileOperationService.RenameAsync(selected.Path, nextName);
            SetStatus(result.Status == FileOperationStatus.Renamed
                ? $"已重新命名為 {Path.GetFileName(result.TargetPath)}。"
                : result.Message ?? result.Reason ?? "重新命名已略過。");
            ClearSelection();
            await LoadLibraryAsync();
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Rename selected image failed.");
            SetStatus("重新命名時發生錯誤，已寫入診斷記錄。");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedImages))]
    private async Task TrashSelected()
    {
        try
        {
            if (IsFileOperationActive)
            {
                return;
            }

            var selected = SelectedImages();
            if (selected.Count == 0)
            {
                return;
            }

            var message = selected.Count == 1
                ? $"要將「{selected[0].Name}」移至回收筒嗎？"
                : $"要將選取的 {selected.Count} 張圖片移至回收筒嗎？";
            if (!await dialogService.ConfirmAsync(message, "將選取的圖片移至回收筒", "移至回收筒"))
            {
                return;
            }

            var operation = BeginFileOperation();
            SetStatus($"正在將 {selected.Count} 張圖片移至回收筒…");
            var results = new List<FileOperationResult>(selected.Count);
            try
            {
                foreach (var image in selected)
                {
                    operation.Token.ThrowIfCancellationRequested();
                    results.Add(await fileOperationService.TrashAsync(image.Path, operation.Token));
                }

                var batch = new FileOperationBatchResult(results);
                SetBatchStatus("移至回收筒", batch);
                ClearSelection();
                await LoadLibraryAsync();
            }
            catch (OperationCanceledException)
            {
                SetStatus("已取消移至回收筒。");
            }
            finally
            {
                EndFileOperation(operation);
            }
        }
        catch (Exception ex)
        {
            appLogger.Error(ex, "Trash selected images failed.");
            SetStatus("移至回收筒時發生錯誤，已寫入診斷記錄。");
        }
    }

    private Task<bool> ConfirmLargeBatchAsync(int count, string messageFormat, string title, string confirmButtonText) =>
        count < LargeBatchConfirmationThreshold
            ? Task.FromResult(true)
            : dialogService.ConfirmAsync(string.Format(messageFormat, count), title, confirmButtonText);

    private CancellationTokenSource BeginFileOperation()
    {
        fileOperationCancellationSource?.Cancel();
        fileOperationCancellationSource?.Dispose();
        fileOperationCancellationSource = new CancellationTokenSource();
        OnPropertyChanged(nameof(IsFileOperationActive));
        CancelFileOperationCommand.NotifyCanExecuteChanged();
        return fileOperationCancellationSource;
    }

    private void EndFileOperation(CancellationTokenSource operation)
    {
        if (!ReferenceEquals(fileOperationCancellationSource, operation))
        {
            return;
        }

        fileOperationCancellationSource = null;
        operation.Dispose();
        OnPropertyChanged(nameof(IsFileOperationActive));
        CancelFileOperationCommand.NotifyCanExecuteChanged();
    }

    private static string DescribeBatchResult(string label, FileOperationBatchResult result) =>
        $"{label}：成功 {result.Succeeded} 個，略過 {result.Skipped} 個，失敗 {result.Failed} 個。";

    private void SetBatchStatus(string label, FileOperationBatchResult result) =>
        SetStatus(DescribeBatchResult(label, result));
}
