using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PicLens.ViewModels;
using static PicLens.Core.Domain.PathRules;

namespace PicLens.Views;

public partial class MainView
{
    private void LibraryTile_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: LibraryTileItem item } || item.IsFolder)
        {
            return;
        }

        var point = e.GetCurrentPoint(LibraryGrid);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        pointerDragSource = item;
        pointerDragStartPosition = point.Position;
        libraryDragLastPosition = point.Position;
        pointerDragStarted = false;
        pointerDragItems = [];
    }

    private void LibraryTile_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (pointerDragSource is null)
        {
            return;
        }

        var point = e.GetPosition(LibraryGrid);
        libraryDragLastPosition = point;

        if (!pointerDragStarted)
        {
            var distance = point - pointerDragStartPosition;
            if (Math.Abs(distance.X) < PointerDragThreshold && Math.Abs(distance.Y) < PointerDragThreshold)
            {
                return;
            }

            pointerDragStarted = true;
            pointerDragItems = DragItemsFor(pointerDragSource);
            ViewModel.BeginImageDrag(pointerDragItems);
            if (sender is Control control)
            {
                e.Pointer.Capture(control);
            }
        }

        UpdateDragPreview(e.GetPosition(this));
        SetDropRenameTarget(DropRenameTargetAt(point));
        UpdateLibraryDragAutoScroll(point);
        e.Handled = true;
    }

    private async void LibraryTile_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!pointerDragStarted)
        {
            ClearPointerDrag();
            return;
        }

        var target = DropRenameTargetAt(e.GetPosition(LibraryGrid)) ?? currentDropRenameTarget;
        ClearPointerDrag();
        if (target is not null)
        {
            await ViewModel.DropDraggedImagesOnAsync(target);
            QueueRealizedThumbnailLoads();
        }
    }

    private void LibraryTile_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => ClearPointerDrag(false);

    private IReadOnlyList<LibraryTileItem> DragItemsFor(LibraryTileItem source)
    {
        if (SelectedLibraryTiles().Any(item => PathEquals(item.Path, source.Path)))
        {
            return OrderedSelectedLibraryItems().Where(item => !item.IsFolder).ToList();
        }

        return source.IsFolder ? [] : [source];
    }

    private LibraryTileItem? DropRenameTargetAt(Avalonia.Point position)
    {
        if (pointerDragSource is null)
        {
            return null;
        }

        var hit = LibraryGrid.InputHitTest(position) as StyledElement;
        var item = FindLibraryTileItem(hit);
        return item is not null && CanDropDraggedItem(pointerDragSource, item) ? item : null;
    }

    private static bool CanDropDraggedItem(LibraryTileItem source, LibraryTileItem target) =>
        !source.IsFolder
        && !target.IsFolder
        && !PathEquals(source.Path, target.Path);

    private void SetDropRenameTarget(LibraryTileItem? target)
    {
        if (ReferenceEquals(currentDropRenameTarget, target))
        {
            return;
        }

        if (currentDropRenameTarget is not null)
        {
            currentDropRenameTarget.IsDropRenameTarget = false;
        }

        currentDropRenameTarget = target;
        if (currentDropRenameTarget is not null)
        {
            currentDropRenameTarget.IsDropRenameTarget = true;
        }
    }

    private void ClearPointerDrag(bool releaseCapture = true)
    {
        if (releaseCapture)
        {
            pointerDragSource = null;
        }

        pointerDragStarted = false;
        pointerDragItems = [];
        SetDropRenameTarget(null);
        HideDragPreview();
        StopLibraryDragAutoScroll();
    }

    private void UpdateDragPreview(Avalonia.Point position)
    {
        var count = pointerDragItems.Count;
        LibraryDragPreviewText.Text = count <= 1 ? "拖曳 1 張圖片" : $"拖曳 {count} 張圖片";
        libraryDragPreviewTransform.X = position.X + 12;
        libraryDragPreviewTransform.Y = position.Y + 12;
        LibraryDragPreviewOverlay.IsVisible = true;
    }

    private void HideDragPreview()
    {
        LibraryDragPreviewOverlay.IsVisible = false;
    }

    private void UpdateLibraryDragAutoScroll(Avalonia.Point position)
    {
        libraryDragLastPosition = position;
        if (DragInteractionRules.CalculateLibraryDragAutoScrollDelta(position.Y, LibraryGrid.Bounds.Height) == 0)
        {
            StopLibraryDragAutoScroll();
            return;
        }

        if (!libraryDragAutoScrollTimer.IsEnabled)
        {
            libraryDragAutoScrollTimer.Start();
        }
    }

    private void StopLibraryDragAutoScroll()
    {
        if (libraryDragAutoScrollTimer.IsEnabled)
        {
            libraryDragAutoScrollTimer.Stop();
        }
    }

    private void LibraryDragAutoScrollTimer_Tick(object? sender, EventArgs e)
    {
        var delta = DragInteractionRules.CalculateLibraryDragAutoScrollDelta(libraryDragLastPosition.Y, LibraryGrid.Bounds.Height);
        if (delta == 0)
        {
            StopLibraryDragAutoScroll();
            return;
        }

        LibraryGrid.Offset = LibraryGrid.Offset.WithY(Math.Max(0, LibraryGrid.Offset.Y + delta));
    }

    private void LibraryItemsRepeater_ElementPrepared(object? sender, ItemsRepeaterElementPreparedEventArgs e)
    {
        if (e.Element.DataContext is LibraryTileItem item)
        {
            _ = ViewModel.LoadThumbnailAsync(item);
        }
    }

    private void LibraryItemsRepeater_ElementClearing(object? sender, ItemsRepeaterElementClearingEventArgs e)
    {
        if (e.Element is Control { DataContext: LibraryTileItem item } element)
        {
            foreach (var image in element.GetVisualDescendants().OfType<Image>())
            {
                image.Source = null;
            }

            ViewModel.CancelThumbnailLoad(item);
        }
    }

    private void QueueRealizedThumbnailLoads()
    {
        foreach (var tile in LibraryGrid.GetVisualDescendants().OfType<Border>())
        {
            if (tile.Classes.Contains("tile") && tile.DataContext is LibraryTileItem item)
            {
                _ = ViewModel.LoadThumbnailAsync(item);
            }
        }
    }

    private async void ThumbnailSizeSlider_CommitValue(object? sender, RoutedEventArgs e)
    {
        await CommitThumbnailSizeSliderValueAsync();
    }

    private async void ThumbnailSizeSlider_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await CommitThumbnailSizeSliderValueAsync();
            e.Handled = true;
        }
    }

    private void ThumbnailSizeSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!initialLoadCompleted || e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        thumbnailSizeCommitTimer.Stop();
        thumbnailSizeCommitTimer.Start();
    }

    private async void ThumbnailSizeCommitTimer_Tick(object? sender, EventArgs e)
    {
        thumbnailSizeCommitTimer.Stop();
        await CommitThumbnailSizeSliderValueAsync();
    }

    private async Task CommitThumbnailSizeSliderValueAsync()
    {
        await ViewModel.ChangeThumbnailSizeAsync(ThumbnailSizeSlider.Value);
        QueueRealizedThumbnailLoads();
    }

    private void SelectLibraryTile(LibraryTileItem item, KeyModifiers modifiers)
    {
        var selected = SelectedLibraryTiles().ToList();
        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            SetSelectedLibraryTiles(SelectionRangeTo(item));
            return;
        }

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            if (item.IsSelected)
            {
                selected.RemoveAll(selectedItem => PathEquals(selectedItem.Path, item.Path));
            }
            else
            {
                selected.Add(item);
            }

            SetSelectedLibraryTiles(selected);
            return;
        }

        SetSelectedLibraryTiles([item]);
    }

    private IReadOnlyList<LibraryTileItem> SelectionRangeTo(LibraryTileItem item)
    {
        var anchor = librarySelectionOrder.LastOrDefault();
        var start = anchor is null ? -1 : ViewModel.LibraryItems.IndexOf(anchor);
        var end = ViewModel.LibraryItems.IndexOf(item);
        if (start < 0 || end < 0)
        {
            return [item];
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        return ViewModel.LibraryItems.Skip(start).Take(end - start + 1).ToList();
    }

    private void SetSelectedLibraryTiles(IReadOnlyList<LibraryTileItem> selectedItems)
    {
        var selectedPaths = selectedItems.Select(item => PathKey(item.Path)).ToHashSet(PathComparer);
        foreach (var item in ViewModel.LibraryItems)
        {
            item.IsSelected = selectedPaths.Contains(PathKey(item.Path));
        }

        librarySelectionOrder.RemoveAll(item => !selectedPaths.Contains(PathKey(item.Path)));
        foreach (var item in selectedItems)
        {
            if (!librarySelectionOrder.Any(existing => PathEquals(existing.Path, item.Path)))
            {
                librarySelectionOrder.Add(item);
            }
        }

        ViewModel.UpdateSelectedLibraryItems(OrderedSelectedLibraryItems());
    }

    private IReadOnlyList<LibraryTileItem> OrderedSelectedLibraryItems()
    {
        var selectedItems = SelectedLibraryTiles().ToList();
        var selectedPaths = selectedItems.Select(item => PathKey(item.Path)).ToHashSet(PathComparer);
        librarySelectionOrder.RemoveAll(item => !selectedPaths.Contains(PathKey(item.Path)));

        var ordered = librarySelectionOrder
            .Where(item => selectedPaths.Contains(PathKey(item.Path)))
            .ToList();

        foreach (var item in selectedItems)
        {
            if (!ordered.Any(existing => PathEquals(existing.Path, item.Path)))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    private static LibraryTileItem? FindLibraryTileItem(object? originalSource)
    {
        var current = originalSource as StyledElement;
        while (current is not null)
        {
            if (current.DataContext is LibraryTileItem item)
            {
                return item;
            }

            current = current is Visual visual ? visual.GetVisualParent() as StyledElement : null;
        }

        return null;
    }
}
