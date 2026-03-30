using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RecordingPathBuilderTests
{
    [Fact]
    public void SanitizeFileNamePart_ReplacesInvalidCharactersAndTrimsTrailingPeriod()
    {
        var value = RecordingPathBuilder.SanitizeFileNamePart(" a<b>:c?. ");

        Assert.Equal("a_b__c_", value);
    }

    [Fact]
    public void Build_UsesTimestampAndLabelForBaseFileName()
    {
        var context = RecordingPathBuilder.Build(
            @"C:\Recordings",
            "しののめ/にこ",
            "video:123",
            new DateTime(2026, 3, 28, 10, 5, 9),
            null);

        Assert.EndsWith(@"20260328_100509_しののめ_にこ_video_123.ts", context.TsPath);
        Assert.EndsWith(@"20260328_100509_しののめ_にこ_video_123.mp4", context.Mp4Path);
        Assert.EndsWith(@"20260328_100509_しののめ_にこ_video_123_ytdlp.log", context.YtDlpLogPath);
        Assert.EndsWith(@"20260328_100509_しののめ_にこ_video_123_ffmpeg.log", context.FfmpegLogPath);
    }
}
