using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiveAlert.Core;
using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RecordingFinalizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
    private RecordingJobContext Context => new("TEST", "test", "https://www.youtube.com/watch?v=test",
        Path.Combine(_root, "out.ts"), Path.Combine(_root, "out.mp4"),
        Path.Combine(_root, "yt.log"), Path.Combine(_root, "ffmpeg.log"), null);

    public RecordingFinalizerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void UnmergedMediaPaths_OnlyIncludesThisAttemptMedia()
    {
        var names = new[] { "out.segment001.f299.mp4", "out.segment001.f140.m4a.part",
            "out.segment001.f140.m4a.ytdl", "out.segment001.f299.mp4.part-Frag1",
            "out.segment002.f299.mp4", "other.segment001.f140.m4a", "out.segment001.recovering.mp4" };
        foreach (var name in names) File.WriteAllText(Path.Combine(_root, name), "data");
        File.WriteAllText(Path.Combine(_root, "out.segment001.f999.webm"), "");
        var found = RecordingFinalizer.GetUnmergedMediaPaths(Path.Combine(_root, "out.segment001.mp4"));
        Assert.Equal(new[] { "out.segment001.f140.m4a.part", "out.segment001.f299.mp4" }, found.Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("{\"streams\":[{\"codec_type\":\"video\"}]}", true, false, 0)]
    [InlineData("{\"streams\":[{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"2.5\"}}", false, true, 2.5)]
    [InlineData("{\"streams\":[{\"codec_type\":\"video\"},{\"codec_type\":\"audio\"}],\"format\":{\"duration\":\"N/A\"}}", true, true, 0)]
    public void MediaInspection_DistinguishesStreamsAndMissingDuration(string json, bool video, bool audio, double duration)
    {
        var info = RecordingMediaInspector.Parse(json);
        Assert.Equal(video, info.HasVideo);
        Assert.Equal(audio, info.HasAudio);
        Assert.Equal(duration, info.Duration);
    }

    [RecordingToolsFact]
    public void Recovery_FinalizesSeparatedVideoAndPartialAudioWithoutDeletingInputs()
    {
        var segment = RecordingJob.BuildAttemptSegmentPath(Context, 1);
        var video = Path.Combine(_root, "out.segment001.f299.webm");
        var audio = Path.Combine(_root, "out.segment001.f140.mp4.part");
        RunFfmpeg($"-f lavfi -i testsrc2=size=64x64:rate=10 -t 1 -an -c:v libvpx-vp9 \"{video}\"");
        RunFfmpeg($"-f lavfi -i sine=frequency=440 -t 1 -vn -c:a aac -f mp4 \"{audio}\"");
        var finalizer = new RecordingFinalizer();
        Assert.True(finalizer.PrepareSegment(Context, segment, CancellationToken.None));
        var result = finalizer.FinalizeToMp4(Context, new[] { segment }, CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        var info = RecordingMediaInspector.Inspect(Context.Mp4Path, CancellationToken.None);
        Assert.True(info.HasBothStreams);
        Assert.InRange(info.Duration, 0.9, 1.2);
        Assert.True(File.Exists(video));
        Assert.True(File.Exists(audio));
        Assert.True(File.Exists(segment));
        RunFfmpeg($"-ss 0.5 -i \"{Context.Mp4Path}\" -t 0.1 -map 0:v:0 -map 0:a:0 -f null -");
        Assert.Matches(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] \[stderr\]", File.ReadAllText(Context.FfmpegLogPath));
    }

    [RecordingToolsFact]
    public async Task RecordingJob_RecoversExitOneAndOnlyCleansInputsAfterCompletion()
    {
        var video = Path.Combine(_root, "out.segment001.f299.webm");
        var audio = Path.Combine(_root, "out.segment001.f140.mp4.part");
        RunFfmpeg($"-f lavfi -i testsrc2=size=64x64:rate=10 -t 1 -an -c:v libvpx-vp9 \"{video}\"");
        RunFfmpeg($"-f lavfi -i sine=frequency=440 -t 1 -vn -c:a aac -f mp4 \"{audio}\"");
        var runner = new YtDlpRunner((_, _) => ProcessExecutionHelper.Start("cmd.exe", "/d /c exit 1"));
        var job = new RecordingJob(Context, runner, new RecordingFinalizer(), new EndedDetector(),
            new RecordingProcessController(), _ => { }, () => { }, CancellationToken.None);
        await job.RunAsync();
        Assert.True(RecordingMediaInspector.Inspect(Context.Mp4Path, CancellationToken.None).HasBothStreams);
        Assert.False(File.Exists(video));
        Assert.False(File.Exists(audio));
        Assert.False(File.Exists(RecordingJob.BuildAttemptSegmentPath(Context, 1)));
    }

    private sealed class EndedDetector : ILiveDetector
    {
        public Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken token) => Task.FromResult(LiveCheckResult.NotLive());
    }

    [RecordingToolsFact]
    public void Recovery_RejectsMissingAudioAndPreservesVideo()
    {
        var video = Path.Combine(_root, "out.segment001.f299.webm");
        RunFfmpeg($"-f lavfi -i testsrc2=size=64x64:rate=10 -t 1 -an -c:v libvpx-vp9 \"{video}\"");
        Assert.Throws<InvalidDataException>(() => new RecordingFinalizer().PrepareSegment(
            Context, RecordingJob.BuildAttemptSegmentPath(Context, 1), CancellationToken.None));
        Assert.True(File.Exists(video));
        Assert.False(File.Exists(Context.Mp4Path));
    }

    [RecordingToolsFact]
    public void Finalization_RejectsVideoOnlyAndPreservesInput()
    {
        var segment = RecordingJob.BuildAttemptSegmentPath(Context, 1);
        RunFfmpeg($"-f lavfi -i testsrc2=size=64x64:rate=10 -t 1 -an -c:v mpeg4 \"{segment}\"");
        var result = new RecordingFinalizer().FinalizeToMp4(Context, new[] { segment }, CancellationToken.None);
        Assert.NotEqual(0, result.ExitCode);
        Assert.True(File.Exists(segment));
        Assert.False(File.Exists(Context.Mp4Path));
    }

    private static void RunFfmpeg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("ffmpeg", "-hide_banner -v error -n " + arguments)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true });
        Assert.NotNull(process);
        var error = process!.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(15000), "ffmpeg timed out");
        Assert.True(process.ExitCode == 0, error);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}

public sealed class RecordingToolsFactAttribute : FactAttribute
{
    public RecordingToolsFactAttribute()
    {
        foreach (var command in new[] { "ffmpeg", "ffprobe" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo(command, "-version")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
                if (process is null || !process.WaitForExit(5000) || process.ExitCode != 0)
                    Skip = $"{command} is required for media integration tests";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Skip = $"{command} is required for media integration tests";
            }
        }
    }
}
