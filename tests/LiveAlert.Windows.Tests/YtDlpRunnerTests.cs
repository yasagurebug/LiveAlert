using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class YtDlpRunnerTests
{
    [Fact]
    public void BuildArguments_RequestsBestVideoAndBestAudio()
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

        Assert.Contains("-f \"bestvideo*+bestaudio/best\"", arguments);
        Assert.Contains("--no-progress", arguments);
        Assert.Contains("--downloader-args \"ffmpeg_i:-loglevel error\"", arguments);
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
}
