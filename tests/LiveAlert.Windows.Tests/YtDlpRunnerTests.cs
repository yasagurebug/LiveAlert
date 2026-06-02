using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class YtDlpRunnerTests
{
    [Fact]
    public void BuildArguments_UsesDashLiveFromStartRecording()
    {
        var context = new RecordingJobContext(
            "ALPHA",
            "video123",
            "https://www.youtube.com/watch?v=video123",
            @"C:\Recordings\out.ts",
            @"C:\Recordings\out.mp4",
            @"C:\Recordings\out_ytdlp.log",
            @"C:\Recordings\out_ffmpeg.log",
            null);

        var arguments = YtDlpRunner.BuildArguments(context);

        Assert.Contains("--live-from-start", arguments);
        Assert.Contains("-f \"(bv*+ba)/b\"", arguments);
        Assert.Contains("--no-progress", arguments);
        Assert.Contains("--merge-output-format mp4", arguments);
        Assert.DoesNotContain("--hls-use-mpegts", arguments);
        Assert.Contains("-o \"C:\\Recordings\\out.ts\"", arguments);
        Assert.Contains("\"https://www.youtube.com/watch?v=video123\"", arguments);
    }

    [Fact]
    public void BuildArguments_IncludesCookiesWhenPresent()
    {
        var context = new RecordingJobContext(
            "ALPHA",
            "video123",
            "https://www.youtube.com/watch?v=video123",
            @"C:\Recordings\out.ts",
            @"C:\Recordings\out.mp4",
            @"C:\Recordings\out_ytdlp.log",
            @"C:\Recordings\out_ffmpeg.log",
            @"C:\Users\main\AppData\Roaming\LiveAlert\cookies.txt");

        var arguments = YtDlpRunner.BuildArguments(context);

        Assert.Contains("--cookies \"C:\\Users\\main\\AppData\\Roaming\\LiveAlert\\cookies.txt\"", arguments);
    }

    [Fact]
    public void BuildArguments_UsesExplicitOutputPath()
    {
        var context = new RecordingJobContext(
            "ALPHA",
            "video123",
            "https://www.youtube.com/watch?v=video123",
            @"C:\Recordings\out.ts",
            @"C:\Recordings\out.mp4",
            @"C:\Recordings\out_ytdlp.log",
            @"C:\Recordings\out_ffmpeg.log",
            null);

        var arguments = YtDlpRunner.BuildArguments(context, @"C:\Recordings\out.segment002.%(ext)s");

        Assert.Contains("-o \"C:\\Recordings\\out.segment002.%(ext)s\"", arguments);
        Assert.DoesNotContain("-o \"C:\\Recordings\\out.ts\"", arguments);
    }
}
