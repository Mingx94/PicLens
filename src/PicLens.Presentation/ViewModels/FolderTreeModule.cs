using PicLens.Core.Models;
using PicLens.Core.Services;
using PicLens.Diagnostics;
using System.Collections.ObjectModel;
using static PicLens.Core.Domain.PathRules;
using static PicLens.ViewModels.ViewModelPathRules;

namespace PicLens.ViewModels;

internal sealed class FolderTreeModule(IFolderScanner folderScanner, IAppLogger appLogger)
{
    private string rootPath = string.Empty;

    public ObservableCollection<FolderTreeItem> Roots { get; } = [];

    public string RootPath => rootPath;

    public void UseRoot(string path)
    {
        rootPath = path;
    }

    public string EnsureRoot(string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = fallbackPath;
        }

        return rootPath;
    }

    public void Clear()
    {
        rootPath = string.Empty;
        Roots.Clear();
    }

    public bool IsDisplayedRootChanged(string nextRootPath)
    {
        var existingRoot = Roots.FirstOrDefault();
        return existingRoot is null || !PathEquals(existingRoot.Path, nextRootPath);
    }

    public void ShowPendingRoot(string rootPath, string selectedPath)
    {
        var root = CreateRoot(rootPath, selectedPath);
        root.Children.Add(new FolderTreeItem("", "", isReadable: false));
        Roots.Clear();
        Roots.Add(root);
    }

    public async Task<FolderTreeItem> BuildRootAsync(
        string rootPath,
        string selectedPath,
        CancellationToken cancellationToken)
    {
        var root = CreateRoot(rootPath, selectedPath);
        await PopulateChildrenAsync(root, rootPath, selectedPath, cancellationToken);
        return root;
    }

    public void ReplaceRoot(FolderTreeItem root)
    {
        Roots.Clear();
        Roots.Add(root);
    }

    public async Task LoadChildrenOnDemandAsync(FolderTreeItem node, string selectedPath)
    {
        if (node.HasLoadedChildren)
        {
            return;
        }

        try
        {
            var folders = await folderScanner.ScanChildFoldersAsync(node.Path, CancellationToken.None);

            node.Children.Clear();
            foreach (var folder in folders)
            {
                var child = CreateChild(folder, selectedPath);
                if (child.IsExpanded)
                {
                    await PopulateChildrenAsync(child, folder.Path, selectedPath, CancellationToken.None);
                }
                else
                {
                    AddPendingChild(child);
                }

                node.Children.Add(child);
            }

            node.HasLoadedChildren = true;
        }
        catch (Exception ex)
        {
            appLogger.Error(
                ex,
                $"Lazy load folder children failed. FolderPath={node.Path}; FolderTreeRootPath={rootPath}; CurrentFolderPath={selectedPath}");
        }
    }

    public void SelectPath(string selectedPath)
    {
        foreach (var root in Roots)
        {
            SelectPath(root, selectedPath);
        }
    }

    private async Task PopulateChildrenAsync(
        FolderTreeItem node,
        string folderPath,
        string selectedPath,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FolderListItem> folders;
        try
        {
            folders = await folderScanner.ScanChildFoldersAsync(folderPath, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            appLogger.Error(
                ex,
                $"Load folder tree children failed. FolderPath={folderPath}; FolderTreeRootPath={rootPath}; CurrentFolderPath={selectedPath}");
            return;
        }

        node.Children.Clear();
        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var child = CreateChild(folder, selectedPath);
            node.Children.Add(child);

            if (child.IsExpanded)
            {
                await PopulateChildrenAsync(child, folder.Path, selectedPath, cancellationToken);
            }
            else
            {
                AddPendingChild(child);
            }
        }

        node.HasLoadedChildren = true;
    }

    private static FolderTreeItem CreateRoot(string rootPath, string selectedPath) =>
        new(
            FolderDisplayName(rootPath),
            rootPath,
            isReadable: Directory.Exists(rootPath),
            isExpanded: true,
            isSelected: PathEquals(rootPath, selectedPath));

    private static FolderTreeItem CreateChild(FolderListItem folder, string selectedPath) =>
        new(
            folder.Name,
            folder.Path,
            isReadable: true,
            isExpanded: IsPathAncestorOrEqual(folder.Path, selectedPath),
            isSelected: PathEquals(folder.Path, selectedPath));

    private static void AddPendingChild(FolderTreeItem node)
    {
        node.Children.Add(new FolderTreeItem("", "", isReadable: false));
    }

    private bool SelectPath(FolderTreeItem node, string selectedPath)
    {
        if (string.IsNullOrEmpty(node.Path))
        {
            return false;
        }

        var isSelected = PathEquals(node.Path, selectedPath);
        var isAncestor = IsPathAncestor(node.Path, selectedPath);

        node.IsSelected = isSelected;

        if (isSelected || isAncestor)
        {
            node.IsExpanded = true;
            if (isAncestor && !node.HasLoadedChildren)
            {
                _ = LoadChildrenOnDemandAsync(node, selectedPath);
            }
        }

        var anyChildSelectedOrAncestor = false;
        foreach (var child in node.Children)
        {
            if (SelectPath(child, selectedPath))
            {
                anyChildSelectedOrAncestor = true;
            }
        }

        return isSelected || isAncestor || anyChildSelectedOrAncestor;
    }
}
