# Contextual Action Bar Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a contextual selected-image action bar to the main WinUI shell while preserving existing file-operation behavior.

**Architecture:** Keep `GridView` as the source of visual selection and keep `MainPageViewModel` as the source of selection-derived state. Move single-selection commands out of the global library toolbar into a contextual row that appears only when images are selected; keep bottom `InfoBar` for operation results. Apply only a light viewer status-bar polish so the secondary viewer remains a fast confirmation surface.

**Tech Stack:** WinUI 3 XAML, CommunityToolkit.Mvvm, xUnit, PowerShell, existing `ImageViewerWin.ViewModels.Tests`.

---

## File Structure

- Modify `ImageViewerWin/ViewModels/MainPageViewModel.cs`: add selection-derived properties, public clear method, and property-change notifications.
- Modify `ImageViewerWin/MainPage.xaml`: remove selection-only commands from the global command bar and add the contextual selected-image action bar.
- Modify `ImageViewerWin/MainPage.xaml.cs`: add the `ClearLibrarySelection_Click` handler that clears both `GridView` and ViewModel selection state.
- Modify `ImageViewerWin/ImageViewerWindow.xaml`: add explicit status-bar text styles for quick confirmation readability.
- Create `tests/ImageViewerWin.ViewModels.Tests/MainPageSelectionTests.cs`: behavior tests for selection-derived ViewModel state and command availability.
- Modify `tests/ImageViewerWin.ViewModels.Tests/MainPageTextTests.cs`: XAML/code-behind contract tests for contextual action bar and toolbar command placement.
- Modify `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWindowLocalizationTests.cs`: contract test for viewer status-bar quick-confirmation styling.

## Task 1: Add Failing Selection State Tests

**Files:**
- Create: `tests/ImageViewerWin.ViewModels.Tests/MainPageSelectionTests.cs`
- Test: `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWin.ViewModels.Tests.csproj`

- [ ] **Step 1: Create ViewModel selection tests**

Create `tests/ImageViewerWin.ViewModels.Tests/MainPageSelectionTests.cs` with this content:

```csharp
using ImageViewerWin.Application.Services;
using ImageViewerWin.Core.Models;
using ImageViewerWin.ViewModels;

namespace ImageViewerWin.ViewModels.Tests;

public sealed class MainPageSelectionTests
{
    [Fact]
    public void Selection_state_defaults_to_empty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("未選取圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_tracks_one_selected_image()
    {
        var viewModel = CreateViewModel();
        var image = ImageTile("a.jpg", @"C:\Album\a.jpg");

        viewModel.UpdateSelectedLibraryItems([image]);

        Assert.Equal(1, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.True(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 1 張圖片", viewModel.SelectionSummaryText);
        Assert.True(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.True(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_tracks_multiple_selected_images_without_enabling_single_image_commands()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateSelectedLibraryItems(
        [
            ImageTile("a.jpg", @"C:\Album\a.jpg"),
            ImageTile("b.png", @"C:\Album\b.png")
        ]);

        Assert.Equal(2, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 2 張圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Selection_state_ignores_selected_folders()
    {
        var viewModel = CreateViewModel();

        viewModel.UpdateSelectedLibraryItems(
        [
            FolderTile("Nested", @"C:\Album\Nested"),
            ImageTile("a.jpg", @"C:\Album\a.jpg")
        ]);

        Assert.Equal(1, viewModel.SelectedImageCount);
        Assert.True(viewModel.HasSelectedImages);
        Assert.True(viewModel.HasSingleSelectedImage);
        Assert.Equal("已選 1 張圖片", viewModel.SelectionSummaryText);
    }

    [Fact]
    public void ClearSelectedLibraryItems_resets_selection_state()
    {
        var viewModel = CreateViewModel();
        viewModel.UpdateSelectedLibraryItems([ImageTile("a.jpg", @"C:\Album\a.jpg")]);

        viewModel.ClearSelectedLibraryItems();

        Assert.Equal(0, viewModel.SelectedImageCount);
        Assert.False(viewModel.HasSelectedImages);
        Assert.False(viewModel.HasSingleSelectedImage);
        Assert.Equal("未選取圖片", viewModel.SelectionSummaryText);
        Assert.False(viewModel.RenameSelectedCommand.CanExecute(null));
        Assert.False(viewModel.TrashSelectedCommand.CanExecute(null));
    }

    private static MainPageViewModel CreateViewModel() =>
        new(
            new ThrowingSettingsStore(),
            new ThrowingFolderScanner(),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            () => Task.FromResult<string?>(null),
            (_, _, _) => Task.FromResult(false),
            _ => Task.FromResult<string?>(null),
            _ => { });

    private static LibraryTileItem ImageTile(string name, string path) =>
        new(
            Name: name,
            Path: path,
            Detail: "JPG - 1 KB",
            IsFolder: false,
            IsSelected: false,
            IsAnimated: false,
            IconGlyph: "\uEB9F",
            SourceItem: new ImageListItem($"image:{name}", path, name, Path.GetExtension(name), 1, 1024));

    private static LibraryTileItem FolderTile(string name, string path) =>
        new(
            Name: name,
            Path: path,
            Detail: "開啟資料夾",
            IsFolder: true,
            IsSelected: false,
            IsAnimated: false,
            IconGlyph: "\uE8B7",
            SourceItem: new FolderListItem($"folder:{name}", path, name, 1));

    private sealed class ThrowingSettingsStore : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AppSettings> UpdateAsync(AppSettingsPatch patch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFolderScanner : IFolderScanner
    {
        public Task<IReadOnlyList<ListItem>> ScanAsync(
            ListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FolderListItem>> ScanChildFoldersAsync(
            string folderPath,
            SortState sort,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingFileOperationService : IFileOperationService
    {
        public Task<FileOperationBatchResult> ConvertVisibleToJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationBatchResult> TrashSameBasenameNonJpgAsync(
            IEnumerable<ImageListItem> visibleImages,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationResult> TrashAsync(string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationResult> RenameAsync(
            string sourcePath,
            string newFileName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FileOperationBatchResult> RenameByDropTargetAsync(
            IEnumerable<string> sourcePaths,
            string targetPath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NullThumbnailService : IThumbnailService
    {
        public Task<string?> GetOrCreateThumbnailAsync(
            string imagePath,
            int requestedSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
```

- [ ] **Step 2: Run the new tests and verify they fail for missing selection properties**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter MainPageSelectionTests
```

Expected: FAIL at compile time with errors for missing `SelectedImageCount`, `HasSelectedImages`, `SelectionSummaryText`, and `ClearSelectedLibraryItems`.

## Task 2: Implement ViewModel Selection State

**Files:**
- Modify: `ImageViewerWin/ViewModels/MainPageViewModel.cs`
- Test: `tests/ImageViewerWin.ViewModels.Tests/MainPageSelectionTests.cs`

- [ ] **Step 1: Replace the single-selection property block**

In `ImageViewerWin/ViewModels/MainPageViewModel.cs`, replace:

```csharp
public bool HasSingleSelectedImage => SelectedImages().Count == 1;
```

with:

```csharp
public int SelectedImageCount => selectedImagePaths.Count;

public bool HasSelectedImages => SelectedImageCount > 0;

public bool HasSingleSelectedImage => SelectedImageCount == 1;

public string SelectionSummaryText => SelectedImageCount switch
{
    0 => "未選取圖片",
    1 => "已選 1 張圖片",
    _ => $"已選 {SelectedImageCount} 張圖片"
};
```

- [ ] **Step 2: Add the public clear method**

In `ImageViewerWin/ViewModels/MainPageViewModel.cs`, add this method directly above the existing private `ClearSelection()` method:

```csharp
public void ClearSelectedLibraryItems()
{
    ClearSelection();
}
```

- [ ] **Step 3: Expand selection property notifications**

In `ImageViewerWin/ViewModels/MainPageViewModel.cs`, replace `NotifySelectionCommands()` with:

```csharp
private void NotifySelectionCommands()
{
    OnPropertyChanged(nameof(SelectedImageCount));
    OnPropertyChanged(nameof(HasSelectedImages));
    OnPropertyChanged(nameof(HasSingleSelectedImage));
    OnPropertyChanged(nameof(SelectionSummaryText));
    RenameSelectedCommand.NotifyCanExecuteChanged();
    TrashSelectedCommand.NotifyCanExecuteChanged();
}
```

- [ ] **Step 4: Run selection tests and verify they pass**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter MainPageSelectionTests
```

Expected: PASS for all `MainPageSelectionTests` tests.

- [ ] **Step 5: Commit ViewModel selection state**

Run:

```powershell
git add -- ImageViewerWin\ViewModels\MainPageViewModel.cs tests\ImageViewerWin.ViewModels.Tests\MainPageSelectionTests.cs
git commit -m "Add selected image state to main view model"
```

Expected: commit succeeds and includes only those two files.

## Task 3: Add Failing XAML Contract Tests For The Contextual Bar

**Files:**
- Modify: `tests/ImageViewerWin.ViewModels.Tests/MainPageTextTests.cs`
- Test: `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWin.ViewModels.Tests.csproj`

- [ ] **Step 1: Add a contextual action bar contract test**

In `tests/ImageViewerWin.ViewModels.Tests/MainPageTextTests.cs`, add this test after `MainPage_command_bar_uses_overflow_instead_of_horizontal_scrolling`:

```csharp
[Fact]
public void MainPage_declares_contextual_selection_action_bar()
{
    var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml"));
    var code = File.ReadAllText(Path.Combine(RepositoryRoot(), "ImageViewerWin", "MainPage.xaml.cs"));

    Assert.Contains("AutomationProperties.AutomationId=\"LibrarySelectionActionBar\"", xaml);
    Assert.Contains("Visibility=\"{x:Bind local:MainPage.BoolToVisibility(ViewModel.HasSelectedImages), Mode=OneWay}\"", xaml);
    Assert.Contains("AutomationProperties.AutomationId=\"SelectionSummaryText\"", xaml);
    Assert.Contains("Text=\"{x:Bind ViewModel.SelectionSummaryText, Mode=OneWay}\"", xaml);
    Assert.Contains("AutomationProperties.AutomationId=\"SelectionRenameButton\"", xaml);
    Assert.Contains("Command=\"{x:Bind ViewModel.RenameSelectedCommand}\"", xaml);
    Assert.Contains("AutomationProperties.AutomationId=\"SelectionTrashButton\"", xaml);
    Assert.Contains("Command=\"{x:Bind ViewModel.TrashSelectedCommand}\"", xaml);
    Assert.Contains("AutomationProperties.AutomationId=\"SelectionClearButton\"", xaml);
    Assert.Contains("Click=\"ClearLibrarySelection_Click\"", xaml);
    Assert.Contains("ClearLibrarySelection_Click", code);
    Assert.Contains("LibraryGrid.SelectedItems.Clear();", code);
    Assert.Contains("librarySelectionOrder.Clear();", code);
    Assert.Contains("ViewModel.ClearSelectedLibraryItems();", code);
}
```

- [ ] **Step 2: Update the existing header-toolbar test expectations**

In `MainPagePromotesLibraryActionsIntoHeaderToolbar`, replace these assertions:

```csharp
Assert.Contains("AutomationProperties.AutomationId=\"TitleBarRenameSelectedButton\"", xaml);
Assert.Contains("Label=\"重新命名\"", xaml);
Assert.Contains("AutomationProperties.AutomationId=\"TitleBarTrashSelectedButton\"", xaml);
Assert.Contains("Label=\"移至回收筒\"", xaml);
```

with:

```csharp
Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarRenameSelectedButton\"", xaml);
Assert.DoesNotContain("AutomationProperties.AutomationId=\"TitleBarTrashSelectedButton\"", xaml);
Assert.Contains("AutomationProperties.AutomationId=\"SelectionRenameButton\"", xaml);
Assert.Contains("AutomationProperties.AutomationId=\"SelectionTrashButton\"", xaml);
Assert.Contains("AutomationProperties.AutomationId=\"SelectionClearButton\"", xaml);
```

- [ ] **Step 3: Update toolbar tooltip expectations**

In `MainPageToolbarCommandButtonsExposeToolTips`, replace:

```csharp
Assert.Contains("ToolTipService.ToolTip=\"重新命名\"", xaml);
Assert.Contains("ToolTipService.ToolTip=\"移至回收筒\"", xaml);
```

with:

```csharp
Assert.Contains("ToolTipService.ToolTip=\"重新命名選取的圖片\"", xaml);
Assert.Contains("ToolTipService.ToolTip=\"將選取的圖片移至回收筒\"", xaml);
Assert.Contains("ToolTipService.ToolTip=\"清除選取\"", xaml);
```

- [ ] **Step 4: Run the XAML contract tests and verify they fail**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter "MainPage_declares_contextual_selection_action_bar|MainPagePromotesLibraryActionsIntoHeaderToolbar|MainPageToolbarCommandButtonsExposeToolTips"
```

Expected: FAIL because `MainPage.xaml` and `MainPage.xaml.cs` do not yet declare the contextual action bar or clear-selection handler.

## Task 4: Implement The MainPage Contextual Action Bar

**Files:**
- Modify: `ImageViewerWin/MainPage.xaml`
- Modify: `ImageViewerWin/MainPage.xaml.cs`
- Test: `tests/ImageViewerWin.ViewModels.Tests/MainPageTextTests.cs`

- [ ] **Step 1: Expand the library content rows**

In `ImageViewerWin/MainPage.xaml`, change `LibraryContent` row definitions from:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
</Grid.RowDefinitions>
```

to:

```xml
<Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
</Grid.RowDefinitions>
```

- [ ] **Step 2: Remove selection-only commands from the global library toolbar**

In `ImageViewerWin/MainPage.xaml`, remove these two `AppBarButton` blocks from `LibraryCommandBar`:

```xml
<AppBarButton
    x:Name="TitleBarRenameSelectedButton"
    AutomationProperties.AutomationId="TitleBarRenameSelectedButton"
    AutomationProperties.Name="重新命名"
    ToolTipService.ToolTip="重新命名"
    Command="{x:Bind ViewModel.RenameSelectedCommand}"
    Icon="Edit"
    Label="重新命名" />
<AppBarButton
    x:Name="TitleBarTrashSelectedButton"
    AutomationProperties.AutomationId="TitleBarTrashSelectedButton"
    AutomationProperties.Name="移至回收筒"
    ToolTipService.ToolTip="移至回收筒"
    Command="{x:Bind ViewModel.TrashSelectedCommand}"
    Icon="Delete"
    Label="移至回收筒" />
```

- [ ] **Step 3: Insert the contextual action bar**

In `ImageViewerWin/MainPage.xaml`, insert this `Border` after the header `Grid` that contains `LibraryCommandBar` and before `LibraryGrid`:

```xml
<Border
    x:Name="LibrarySelectionActionBar"
    Grid.Row="1"
    Padding="12,8"
    AutomationProperties.AutomationId="LibrarySelectionActionBar"
    Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
    BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
    BorderThickness="1"
    CornerRadius="{ThemeResource ControlCornerRadius}"
    Visibility="{x:Bind local:MainPage.BoolToVisibility(ViewModel.HasSelectedImages), Mode=OneWay}">
    <Grid ColumnSpacing="12">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>

        <StackPanel
            VerticalAlignment="Center"
            Orientation="Horizontal"
            Spacing="8">
            <FontIcon
                FontFamily="{StaticResource SymbolThemeFontFamily}"
                FontSize="16"
                Glyph="&#xE762;" />
            <TextBlock
                x:Name="SelectionSummaryText"
                AutomationProperties.AutomationId="SelectionSummaryText"
                VerticalAlignment="Center"
                Text="{x:Bind ViewModel.SelectionSummaryText, Mode=OneWay}"
                Style="{StaticResource BodyStrongTextBlockStyle}" />
        </StackPanel>

        <CommandBar
            Grid.Column="1"
            AutomationProperties.AutomationId="SelectionCommandBar"
            Background="Transparent"
            DefaultLabelPosition="Right"
            IsDynamicOverflowEnabled="True">
            <AppBarButton
                x:Name="SelectionRenameButton"
                AutomationProperties.AutomationId="SelectionRenameButton"
                AutomationProperties.Name="重新命名選取的圖片"
                ToolTipService.ToolTip="重新命名選取的圖片"
                Command="{x:Bind ViewModel.RenameSelectedCommand}"
                Icon="Edit"
                Label="重新命名" />
            <AppBarButton
                x:Name="SelectionTrashButton"
                AutomationProperties.AutomationId="SelectionTrashButton"
                AutomationProperties.Name="將選取的圖片移至回收筒"
                ToolTipService.ToolTip="將選取的圖片移至回收筒"
                Command="{x:Bind ViewModel.TrashSelectedCommand}"
                Icon="Delete"
                Label="移至回收筒" />
            <AppBarButton
                x:Name="SelectionClearButton"
                AutomationProperties.AutomationId="SelectionClearButton"
                AutomationProperties.Name="清除選取"
                ToolTipService.ToolTip="清除選取"
                Click="ClearLibrarySelection_Click"
                Label="清除選取">
                <AppBarButton.Icon>
                    <SymbolIcon Symbol="Cancel" />
                </AppBarButton.Icon>
            </AppBarButton>
        </CommandBar>
    </Grid>
</Border>
```

- [ ] **Step 4: Move library grid and empty state to the new content row**

In `ImageViewerWin/MainPage.xaml`, change:

```xml
<GridView
    x:Name="LibraryGrid"
    Grid.Row="1"
```

to:

```xml
<GridView
    x:Name="LibraryGrid"
    Grid.Row="2"
```

Change the empty-state grid from:

```xml
<Grid
    Grid.Row="1"
```

to:

```xml
<Grid
    Grid.Row="2"
```

- [ ] **Step 5: Add the clear-selection handler**

In `ImageViewerWin/MainPage.xaml.cs`, add this method after `LibraryGrid_SelectionChanged`:

```csharp
private void ClearLibrarySelection_Click(object sender, RoutedEventArgs e)
{
    LibraryGrid.SelectedItems.Clear();
    librarySelectionOrder.Clear();
    ViewModel.ClearSelectedLibraryItems();
}
```

- [ ] **Step 6: Run the updated main page tests**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter "MainPage_declares_contextual_selection_action_bar|MainPagePromotesLibraryActionsIntoHeaderToolbar|MainPageToolbarCommandButtonsExposeToolTips|MainPage_xaml_uses_zh_tw_copy"
```

Expected: PASS for the filtered tests.

- [ ] **Step 7: Commit the contextual action bar UI**

Run:

```powershell
git add -- ImageViewerWin\MainPage.xaml ImageViewerWin\MainPage.xaml.cs tests\ImageViewerWin.ViewModels.Tests\MainPageTextTests.cs
git commit -m "Add contextual selection action bar"
```

Expected: commit succeeds and includes only those three files.

## Task 5: Add Viewer Status-Bar Quick Confirmation Styling

**Files:**
- Modify: `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWindowLocalizationTests.cs`
- Modify: `ImageViewerWin/ImageViewerWindow.xaml`
- Test: `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWindowLocalizationTests.cs`

- [ ] **Step 1: Add a failing viewer status-bar style contract test**

In `tests/ImageViewerWin.ViewModels.Tests/ImageViewerWindowLocalizationTests.cs`, add this test after `ViewerFullScreenChromeBindsVisibilityToViewModel`:

```csharp
[Fact]
public void ViewerStatusBarUsesQuickConfirmationTextStyles()
{
    var xaml = ReadRepositoryFile("ImageViewerWin", "ImageViewerWindow.xaml");

    Assert.Contains("AutomationProperties.AutomationId=\"ViewerStatusBar\"", xaml);
    Assert.Contains("Text=\"{x:Bind ViewModel.CurrentImageName, Mode=OneWay}\"", xaml);
    Assert.Contains("Style=\"{StaticResource BodyStrongTextBlockStyle}\"", xaml);
    Assert.Contains("Text=\"{x:Bind ViewModel.ZoomLabel, Mode=OneWay}\"", xaml);
    Assert.Contains("Style=\"{StaticResource CaptionTextBlockStyle}\"", xaml);
}
```

- [ ] **Step 2: Run the viewer status-bar test and verify it fails**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter ViewerStatusBarUsesQuickConfirmationTextStyles
```

Expected: FAIL because the status bar text blocks do not yet declare the required styles.

- [ ] **Step 3: Style the viewer status-bar text**

In `ImageViewerWin/ImageViewerWindow.xaml`, change the status-bar file-name `TextBlock` to:

```xml
<TextBlock
    Grid.Column="0"
    VerticalAlignment="Center"
    Text="{x:Bind ViewModel.CurrentImageName, Mode=OneWay}"
    TextTrimming="CharacterEllipsis"
    Style="{StaticResource BodyStrongTextBlockStyle}" />
```

Change the zoom-label `TextBlock` to:

```xml
<TextBlock
    Grid.Column="1"
    VerticalAlignment="Center"
    Foreground="{ThemeResource TextFillColorSecondaryBrush}"
    Text="{x:Bind ViewModel.ZoomLabel, Mode=OneWay}"
    Style="{StaticResource CaptionTextBlockStyle}" />
```

- [ ] **Step 4: Run the viewer status-bar test and verify it passes**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter ViewerStatusBarUsesQuickConfirmationTextStyles
```

Expected: PASS.

- [ ] **Step 5: Commit viewer status polish**

Run:

```powershell
git add -- ImageViewerWin\ImageViewerWindow.xaml tests\ImageViewerWin.ViewModels.Tests\ImageViewerWindowLocalizationTests.cs
git commit -m "Polish viewer status bar text"
```

Expected: commit succeeds and includes only those two files.

## Task 6: Full Verification

**Files:**
- Verify: `ImageViewerWin.slnx`
- Verify: `ImageViewerWin/ImageViewerWin.csproj`

- [ ] **Step 1: Run the full ViewModels test project**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore
```

Expected: PASS.

- [ ] **Step 2: Run solution tests**

Run:

```powershell
dotnet test .\ImageViewerWin.slnx --no-restore -m:1
```

Expected: PASS. If this fails because restore artifacts or platform assets are unavailable, run each test project directly and record the exact failure text before continuing.

- [ ] **Step 3: Run WinUI build verification**

Run:

```powershell
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj -SkipRun
```

Expected: build completes without launching the app.

- [ ] **Step 4: Check diff hygiene**

Run:

```powershell
git diff --check
git status --short
```

Expected: `git diff --check` prints no errors. `git status --short` shows only intentionally uncommitted files such as pre-existing `.agents/` and `skills-lock.json`, or a clean tree if those were handled outside this plan.

- [ ] **Step 5: Final integration commit if any verification-only fixes were needed**

If verification required a small follow-up fix, run:

```powershell
git add -- ImageViewerWin tests
git commit -m "Verify contextual selection action bar"
```

Expected: commit succeeds only when follow-up code changes exist. If no follow-up changes exist, skip this commit and report that Tasks 2, 4, and 5 commits contain the implementation.
