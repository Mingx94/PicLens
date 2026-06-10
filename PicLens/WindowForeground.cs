using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace PicLens;

internal static class WindowForeground
{
    private const int GWLP_HWNDPARENT = -8;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly nint HWND_TOPMOST = new(-1);
    private static readonly nint HWND_NOTOPMOST = new(-2);

    public static void ActivateOwnedWindow(Window owner, Window window)
    {
        var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        var windowHwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        SetOwner(windowHwnd, ownerHwnd);
        window.Activate();
        BringToForeground(windowHwnd);
        window.DispatcherQueue.TryEnqueue(() => BringToForeground(windowHwnd));
    }

    private static void SetOwner(nint windowHwnd, nint ownerHwnd)
    {
        if (nint.Size == 8)
        {
            _ = SetWindowLongPtr64(windowHwnd, GWLP_HWNDPARENT, ownerHwnd);
            return;
        }

        _ = SetWindowLong32(windowHwnd, GWLP_HWNDPARENT, ownerHwnd.ToInt32());
    }

    private static void BringToForeground(nint windowHwnd)
    {
        _ = SetWindowPos(windowHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        _ = SetWindowPos(windowHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        _ = SetForegroundWindow(windowHwnd);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
