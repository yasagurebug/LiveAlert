using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveAlert.Core;
using LiveAlert.Windows.Services;
using Xunit;

namespace LiveAlert.Windows.Tests;

public sealed class RecordingJobTests
{
    [Fact]
    public void HasRecordedContent_ReturnsTrue_WhenTsFileExistsAndIsNotEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var tsPath = Path.Combine(root, "recording.ts");

        try
        {
            File.WriteAllText(tsPath, "data");

            Assert.True(RecordingJob.HasRecordedContent(tsPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void HasRecordedContent_ReturnsFalse_WhenTsFileIsMissingOrEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var missingPath = Path.Combine(root, "missing.ts");
        var emptyPath = Path.Combine(root, "empty.ts");

        try
        {
            File.WriteAllText(emptyPath, string.Empty);

            Assert.False(RecordingJob.HasRecordedContent(missingPath));
            Assert.False(RecordingJob.HasRecordedContent(emptyPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void IsNonRetriableYtDlpFailure_ReturnsTrue_ForMembersOnlyErrorInLogTail()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var logPath = Path.Combine(root, "recording_ytdlp.log");

        try
        {
            File.WriteAllText(
                logPath,
                "[stderr] ERROR: [youtube] x9Zc4M_mcmw: This video is available to this channel's members on level: ぼたんえび (or any higher level). Join this channel to get access to members-only content and other exclusive perks.",
                new UTF8Encoding(false));

            Assert.True(RecordingJob.IsNonRetriableYtDlpFailure(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void IsNonRetriableYtDlpFailure_ReturnsFalse_ForGeneralFailureLog()
    {
        var root = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var logPath = Path.Combine(root, "recording_ytdlp.log");

        try
        {
            File.WriteAllText(
                logPath,
                "[stderr] ERROR: Unable to download video data: HTTP Error 500: Internal Server Error",
                new UTF8Encoding(false));

            Assert.False(RecordingJob.IsNonRetriableYtDlpFailure(logPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildAttemptSegmentPath_UsesSeparateMp4PathPerAttempt()
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

        Assert.Equal(@"C:\Recordings\out.segment001.mp4", RecordingJob.BuildAttemptSegmentPath(context, 1));
        Assert.Equal(@"C:\Recordings\out.segment002.mp4", RecordingJob.BuildAttemptSegmentPath(context, 2));
        Assert.Equal(@"C:\Recordings\out.segment010.mp4", RecordingJob.BuildAttemptSegmentPath(context, 10));
    }

    [Fact]
    public void BuildAttemptOutputTemplate_UsesYtDlpExtensionPlaceholder()
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

        Assert.Equal(@"C:\Recordings\out.segment001.%(ext)s", RecordingJob.BuildAttemptOutputTemplate(context, 1));
        Assert.Equal(@"C:\Recordings\out.segment002.%(ext)s", RecordingJob.BuildAttemptOutputTemplate(context, 2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LiveCheck_RetriesMonitoringErrorsBeforeReturningKnownState(bool live)
    {
        var calls = 0;
        var detector = new Detector(() => ++calls == 1 ? LiveCheckResult.Error("test network failure") :
            live ? LiveCheckResult.Live("test") : LiveCheckResult.NotLive());
        var job = CreateJob(detector, CancellationToken.None);
        Assert.Equal(live, await job.IsStillLiveAsync());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task LiveCheck_CancellationWhileUnknownDoesNotBecomeNotLive()
    {
        using var cancellation = new CancellationTokenSource();
        var detector = new Detector(() =>
        {
            cancellation.Cancel();
            return LiveCheckResult.Error("test network failure");
        });
        var job = CreateJob(detector, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.IsStillLiveAsync());
    }

    private static RecordingJob CreateJob(ILiveDetector detector, CancellationToken token)
    {
        var context = new RecordingJobContext("TEST", "test", "https://www.youtube.com/watch?v=test",
            "unused.ts", "unused.mp4", "unused-ytdlp.log", "unused-ffmpeg.log", null);
        return new RecordingJob(context, new YtDlpRunner(), new RecordingFinalizer(), detector,
            new RecordingProcessController(), _ => { }, () => { }, token);
    }

    private sealed class Detector : ILiveDetector
    {
        private readonly Func<LiveCheckResult> _check;
        public Detector(Func<LiveCheckResult> check) => _check = check;
        public Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken token) => Task.FromResult(_check());
    }
}
