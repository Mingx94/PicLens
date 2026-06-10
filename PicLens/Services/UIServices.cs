using PicLens.Core.Models;
using System;
using System.Threading.Tasks;

namespace PicLens.Services;

public interface IDialogService
{
    Task<string?> ChooseFolderAsync();
    Task<bool> ConfirmAsync(string message, string title, string confirmButtonText);
    Task<string?> RequestRenameAsync(ImageListItem item);
}

public interface INavigationService
{
    void OpenImageViewer(ImageSequenceSnapshot snapshot);
}

public interface IDispatcherService
{
    bool HasUiThreadAccess { get; }
    bool TryEnqueue(Action action);
}

public sealed class DelegateDialogService : IDialogService
{
    private readonly Func<Task<string?>> chooseFolderAsync;
    private readonly Func<string, string, string, Task<bool>> confirmAsync;
    private readonly Func<ImageListItem, Task<string?>> requestRenameAsync;

    public DelegateDialogService(
        Func<Task<string?>> chooseFolderAsync,
        Func<string, string, string, Task<bool>> confirmAsync,
        Func<ImageListItem, Task<string?>> requestRenameAsync)
    {
        this.chooseFolderAsync = chooseFolderAsync ?? throw new ArgumentNullException(nameof(chooseFolderAsync));
        this.confirmAsync = confirmAsync ?? throw new ArgumentNullException(nameof(confirmAsync));
        this.requestRenameAsync = requestRenameAsync ?? throw new ArgumentNullException(nameof(requestRenameAsync));
    }

    public Task<string?> ChooseFolderAsync() => chooseFolderAsync();

    public Task<bool> ConfirmAsync(string message, string title, string confirmButtonText) =>
        confirmAsync(message, title, confirmButtonText);

    public Task<string?> RequestRenameAsync(ImageListItem item) => requestRenameAsync(item);
}

public sealed class DelegateNavigationService : INavigationService
{
    private readonly Action<ImageSequenceSnapshot> openImageViewer;

    public DelegateNavigationService(Action<ImageSequenceSnapshot> openImageViewer)
    {
        this.openImageViewer = openImageViewer ?? throw new ArgumentNullException(nameof(openImageViewer));
    }

    public void OpenImageViewer(ImageSequenceSnapshot snapshot) => openImageViewer(snapshot);
}

public sealed class DelegateDispatcherService : IDispatcherService
{
    private readonly Func<bool>? hasUiThreadAccess;
    private readonly Func<Action, bool>? tryEnqueueOnUiThread;

    public DelegateDispatcherService(Func<bool>? hasUiThreadAccess, Func<Action, bool>? tryEnqueueOnUiThread)
    {
        this.hasUiThreadAccess = hasUiThreadAccess;
        this.tryEnqueueOnUiThread = tryEnqueueOnUiThread;
    }

    public bool HasUiThreadAccess => hasUiThreadAccess?.Invoke() ?? true;

    public bool TryEnqueue(Action action)
    {
        if (tryEnqueueOnUiThread != null)
        {
            return tryEnqueueOnUiThread(action);
        }
        action();
        return true;
    }
}
