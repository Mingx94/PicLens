using PicLens.Core.Models;
using PicLens.Core.Domain;

namespace PicLens.ViewModels.Tests;

public sealed class AppLoggingTests
{
    [Fact]
    public void MainPageViewModel_path_display_properties_log_invalid_paths()
    {
        var logger = new RecordingAppLogger();
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new TestThumbnailService(),
            new TestDialogService(),
            _ => { },
            appLogger: logger);
        viewModel.CurrentFolderPath = "bad\0path";

        Assert.Equal("資料夾", viewModel.CurrentParentFolderName);
        Assert.Equal("bad\0path", viewModel.CurrentFolderName);

        Assert.Contains(logger.ErrorMessages, error => error.Message.StartsWith("Parent folder name lookup failed.", StringComparison.Ordinal));
        Assert.Contains(logger.ErrorMessages, error => error.Message.StartsWith("Folder segment lookup failed.", StringComparison.Ordinal));
    }

}
