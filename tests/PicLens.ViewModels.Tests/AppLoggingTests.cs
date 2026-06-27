using PicLens.Core.Models;
using PicLens.Core.Domain;
using PicLens.Diagnostics;
using PicLens.Core.Services;

namespace PicLens.ViewModels.Tests;

public sealed class AppLoggingTests
{
    [Fact]
    public void MainPageViewModel_path_display_properties_log_invalid_paths()
    {
        var logger = new RecordingLogger();
        var viewModel = new MainPageViewModel(
            new FakeSettingsStore(AppSettings.CreateDefault()),
            new CountingFolderScanner([]),
            new ThrowingFileOperationService(),
            new NullThumbnailService(),
            new NullDialogService(),
            _ => { },
            appLogger: logger);
        viewModel.CurrentFolderPath = "bad\0path";

        Assert.Equal("資料夾", viewModel.CurrentParentFolderName);
        Assert.Equal("bad\0path", viewModel.CurrentFolderName);

        Assert.Contains(logger.Errors, error => error.Message.StartsWith("Parent folder name lookup failed.", StringComparison.Ordinal));
        Assert.Contains(logger.Errors, error => error.Message.StartsWith("Folder segment lookup failed.", StringComparison.Ordinal));
    }

    private sealed class RecordingLogger : IAppLogger
    {
        public List<(Exception Exception, string Message)> Errors { get; } = [];

        public void Info(string message)
        {
        }

        public void Error(Exception exception, string message) => Errors.Add((exception, message));
    }

}
