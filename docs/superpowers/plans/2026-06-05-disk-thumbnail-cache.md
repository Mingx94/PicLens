# Disk Thumbnail Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace main-grid thumbnail rendering from full-size source image decoding with a bounded, disk-cached thumbnail pipeline.

**Architecture:** Add an application-layer thumbnail service contract and an infrastructure implementation that writes small PNG thumbnails to a deterministic cache folder. The WinUI view model requests thumbnails asynchronously when tiles are materialized, cancels work when tiles are unmaterialized, limits concurrent generation, and only updates a tile if it still represents the same source image and thumbnail size.

**Tech Stack:** WinUI 3, CommunityToolkit.Mvvm, xUnit, Windows.Graphics.Imaging, Windows.Storage.

---

### Task 1: Thumbnail Service Contract And Cache Tests

**Files:**
- Create: `src/ImageViewerWin.Application/Services/IThumbnailService.cs`
- Create: `src/ImageViewerWin.Infrastructure/Services/ThumbnailService.cs`
- Create: `tests/ImageViewerWin.Infrastructure.Tests/ThumbnailServiceTests.cs`

- [ ] **Step 1: Write the failing infrastructure tests**

```csharp
[Fact]
public async Task GetOrCreateThumbnailAsync_returns_cached_path_for_same_source_identity_and_size()
{
    using var temp = TempWorkspace.Create();
    var source = await temp.WriteFileAsync("photo.jpg", ValidImageBytes());
    var cacheRoot = Path.Combine(temp.Root, "cache");
    var service = new ThumbnailService(cacheRoot);

    var first = await service.GetOrCreateThumbnailAsync(source, 200);
    var second = await service.GetOrCreateThumbnailAsync(source, 200);

    Assert.False(string.IsNullOrWhiteSpace(first));
    Assert.Equal(first, second);
    Assert.True(File.Exists(first));
}
```

- [ ] **Step 2: Run the targeted infrastructure test**

Run: `dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore --filter ThumbnailServiceTests`

Expected: FAIL because `ThumbnailService` does not exist yet.

- [ ] **Step 3: Implement the contract and disk cache**

Create `IThumbnailService.GetOrCreateThumbnailAsync(string sourcePath, int requestedSize, CancellationToken cancellationToken = default)`.
Implement `ThumbnailService` with a deterministic cache key from full path, last-write UTC, file length, and normalized requested size. Generate a PNG thumbnail at no more than the requested pixel edge.

- [ ] **Step 4: Run the targeted infrastructure test**

Run: `dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore --filter ThumbnailServiceTests`

Expected: PASS.

### Task 2: ViewModel Async Thumbnail Loading

**Files:**
- Modify: `ImageViewerWin/ViewModels/LibraryTileItem.cs`
- Modify: `ImageViewerWin/ViewModels/MainPageViewModel.cs`
- Modify: `ImageViewerWin/MainPage.xaml.cs`
- Test: `tests/ImageViewerWin.ViewModels.Tests/MainPageViewModelThumbnailSizeTests.cs`

- [ ] **Step 1: Write failing view model tests**

Add a fake `IThumbnailService` that returns `thumb:<file>:<size>` and assert that `LoadThumbnailAsync` updates a still image tile from no thumbnail path to the returned thumbnail path.

- [ ] **Step 2: Run the targeted view model tests**

Run: `dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter MainPageViewModelThumbnailSizeTests /p:Platform=x64`

Expected: FAIL because the view model has no thumbnail service dependency and tile thumbnail paths are immutable.

- [ ] **Step 3: Implement asynchronous thumbnail refresh**

Make `LibraryTileItem.ThumbnailPath` mutable with property notifications for `ThumbnailPath`, `CanShowThumbnail`, and `ShouldShowIcon`. Inject `IThumbnailService` into `MainPageViewModel`. When a tile is loaded, start bounded thumbnail generation for non-animated image tiles; when the tile unloads, cancel its request; only apply the result if the source path and requested size still match.

- [ ] **Step 4: Wire the WinUI page**

Construct `ThumbnailService` in `MainPage.xaml.cs` using a local-app-data cache folder, and keep the existing `CreateBitmapImage` as the final URI adapter for already-small cached thumbnails.

- [ ] **Step 5: Run targeted view model tests**

Run: `dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore --filter MainPageViewModelThumbnailSizeTests /p:Platform=x64`

Expected: PASS.

### Task 3: Documentation And Verification

**Files:**
- Modify: `docs/architecture.md`
- Modify: `docs/native-parity.md`

- [ ] **Step 1: Document thumbnail ownership**

State that the grid uses asynchronous disk-cached thumbnails and that full images are reserved for the secondary viewer.

- [ ] **Step 2: Run fresh verification**

Run:

```powershell
dotnet test .\tests\ImageViewerWin.Core.Tests\ImageViewerWin.Core.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Application.Tests\ImageViewerWin.Application.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.Infrastructure.Tests\ImageViewerWin.Infrastructure.Tests.csproj --no-restore
dotnet test .\tests\ImageViewerWin.ViewModels.Tests\ImageViewerWin.ViewModels.Tests.csproj --no-restore /p:Platform=x64
.\BuildAndRun.ps1 .\ImageViewerWin\ImageViewerWin.csproj -SkipRun
git diff --check
```

Expected: all pass without whitespace errors.
