using Microsoft.UI.Windowing;

namespace PicLens;

internal static class TitleBarLayout
{
    public static void UseStandardCaptionButtonHeight(AppWindow appWindow) =>
        ApplyCaptionButtonHeight(appWindow, TitleBarHeightOption.Standard);

    public static void UseTallCaptionButtonHeight(AppWindow appWindow)
    {
        ApplyCaptionButtonHeight(appWindow, TitleBarHeightOption.Tall);
    }

    private static void ApplyCaptionButtonHeight(AppWindow appWindow, TitleBarHeightOption preferredHeightOption)
    {
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            appWindow.TitleBar.PreferredHeightOption = preferredHeightOption;
            return;
        }

        App.Logger.Error(
            new InvalidOperationException("AppWindow title bar customization is not supported."),
            $"Apply caption button height skipped. PreferredHeightOption={preferredHeightOption}");
    }
}
