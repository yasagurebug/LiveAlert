using LiveAlert.Core;
using LiveAlert.Windows.ViewModels;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void BuildConfig_PreservesRecordingHiddenSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manager = new ConfigManager(Path.Combine(tempDir, "config.json"));
        var viewModel = new MainWindowViewModel(manager);

        viewModel.Load(new ConfigRoot
        {
            Alerts =
            {
                new AlertConfig
                {
                    Url = "https://www.youtube.com/channel/ABC",
                    Label = "ALPHA",
                    Message = "msg"
                }
            },
            Options = new AlertOptions
            {
                PollIntervalSec = 60,
                MaxAlarmDurationSec = 30,
                LoopIntervalSec = 5,
                BandHeightPx = 220,
                BandPosition = "top",
                WindowsAutoStart = true,
                LiveRecordingEnabled = true,
                RecordingSaveDirectory = @"C:\Recordings",
                RecordingRetentionDays = 45,
                LastRecordingCleanupDate = "2026-03-28"
            }
        });

        var config = viewModel.BuildConfig();

        Assert.True(config.Options.LiveRecordingEnabled);
        Assert.Equal(@"C:\Recordings", config.Options.RecordingSaveDirectory);
        Assert.Equal(45, config.Options.RecordingRetentionDays);
        Assert.Equal("2026-03-28", config.Options.LastRecordingCleanupDate);
        Assert.Equal("当該フォルダの内容は45日で削除されます。", viewModel.RecordingRetentionWarningText);
    }
}
