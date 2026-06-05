using Microsoft.UI.Windowing;

namespace ImageViewerWin;

internal static class TitleBarLayout
{
    public static void UseTallCaptionButtonHeight(AppWindow appWindow)
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
    }
}
