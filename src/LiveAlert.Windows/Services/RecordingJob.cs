using System.Diagnostics;
using System.IO;
using System.Text;
using LiveAlert.Core;

namespace LiveAlert.Windows.Services;

public sealed class RecordingJob
{
    private readonly RecordingJobContext _context;
    private readonly YtDlpRunner _ytDlpRunner;
    private readonly RecordingFinalizer _finalizer;
    private readonly ILiveDetector _liveDetector;
    private readonly RecordingProcessController _processController;
    private readonly Action<RecordingJobState> _stateChanged;
    private readonly Action _started;
    private readonly CancellationToken _cancellationToken;
    private readonly object _syncRoot = new();
    private Process? _activeProcess;
    private RecordingStopReason _stopReason;
    private bool _startNotified;

    public RecordingJob(
        RecordingJobContext context,
        YtDlpRunner ytDlpRunner,
        RecordingFinalizer finalizer,
        ILiveDetector liveDetector,
        RecordingProcessController processController,
        Action<RecordingJobState> stateChanged,
        Action started,
        CancellationToken cancellationToken)
    {
        _context = context;
        _ytDlpRunner = ytDlpRunner;
        _finalizer = finalizer;
        _liveDetector = liveDetector;
        _processController = processController;
        _stateChanged = stateChanged;
        _started = started;
        _cancellationToken = cancellationToken;
    }

    public void RequestStop(RecordingStopReason reason)
    {
        Process? processToKill = null;
        lock (_syncRoot)
        {
            if (_stopReason == RecordingStopReason.None)
            {
                _stopReason = reason;
            }

            processToKill = _activeProcess;
        }

        _processController.StopProcessesForRecording(_context);

        if (processToKill is null)
        {
            return;
        }

        try
        {
            if (!processToKill.HasExited)
            {
                processToKill.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Recording stop kill skipped label={_context.Label} videoId={_context.VideoId} reason={ex.Message}");
        }
    }

    public async Task RunAsync()
    {
        var retryCount = 0;
        var attemptCount = 0;
        var segmentPaths = new List<string>();
        while (true)
        {
            if (IsStopRequested())
            {
                AppLog.Info($"Recording stopped label={_context.Label} videoId={_context.VideoId} reason={GetStopReasonText()}");
                break;
            }

            _stateChanged(RecordingJobState.Recording);
            attemptCount++;
            var outputTemplate = BuildAttemptOutputTemplate(_context, attemptCount);
            var segmentPath = BuildAttemptSegmentPath(_context, attemptCount);
            var ytDlpResult = await RunYtDlpAsync(outputTemplate).ConfigureAwait(false);
            if (HasRecordedContent(segmentPath) && !segmentPaths.Contains(segmentPath, StringComparer.OrdinalIgnoreCase))
            {
                segmentPaths.Add(segmentPath);
            }

            if (!ytDlpResult.Started)
            {
                LogFailure("yt-dlp起動失敗", ytDlpResult);
                throw new InvalidOperationException("yt-dlp start failed", ytDlpResult.Exception);
            }

            if (IsStopRequested())
            {
                AppLog.Info($"Recording stopped label={_context.Label} videoId={_context.VideoId} reason={GetStopReasonText()}");
                break;
            }

            if (ytDlpResult.ExitCode is not 0 &&
                !HasRecordedContent(segmentPath) &&
                !HasAnyRecordedContent(segmentPaths) &&
                IsNonRetriableYtDlpFailure(_context.YtDlpLogPath))
            {
                LogFailure("yt-dlp視聴権限エラー", ytDlpResult);
                throw new InvalidOperationException($"yt-dlp exited with code {ytDlpResult.ExitCode}");
            }

            var liveStillRunning = await IsStillLiveAsync().ConfigureAwait(false);
            if (liveStillRunning)
            {
                retryCount++;
                AppLog.Warn(
                    $"Recording retry label={_context.Label} videoId={_context.VideoId} retryCount={retryCount} reason=video is still live after yt-dlp exit");
                _stateChanged(RecordingJobState.Retrying);
                if (IsStopRequested())
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(5), _cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (ytDlpResult.ExitCode is not 0)
            {
                if (!HasAnyRecordedContent(segmentPaths))
                {
                    LogFailure("yt-dlp異常終了", ytDlpResult);
                    throw new InvalidOperationException($"yt-dlp exited with code {ytDlpResult.ExitCode}");
                }

                AppLog.Warn(
                    $"Recording continuing to finalize label={_context.Label} videoId={_context.VideoId} exitCode={ytDlpResult.ExitCode} segmentCount={segmentPaths.Count}");
            }

            break;
        }

        if (!HasAnyRecordedContent(segmentPaths))
        {
            if (IsStopRequested())
            {
                AppLog.Info(
                    $"Recording stopped without finalize label={_context.Label} videoId={_context.VideoId} reason={GetStopReasonText()} tsPath={_context.TsPath}");
                return;
            }

            var result = new ExternalProcessResult(true, 0, string.Empty, string.Empty, _context.YtDlpLogPath);
            LogFailure("録画データ未生成", result);
            throw new InvalidOperationException("recording data was not created");
        }

        _stateChanged(RecordingJobState.Finalizing);
        var finalizerResult = _finalizer.FinalizeToMp4(_context, segmentPaths, _cancellationToken);
        if (!finalizerResult.Started || finalizerResult.ExitCode is not 0)
        {
            LogFailure("mp4マージ失敗", finalizerResult);
            throw new InvalidOperationException($"ffmpeg failed with code {finalizerResult.ExitCode}");
        }

        foreach (var segmentPath in segmentPaths)
        {
            try
            {
                if (File.Exists(segmentPath))
                {
                    File.Delete(segmentPath);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Recording temporary file cleanup skipped path={segmentPath} reason={ex.Message}");
            }
        }

        AppLog.Info($"Recording finished label={_context.Label} videoId={_context.VideoId} output={_context.Mp4Path} segmentCount={segmentPaths.Count} success=true");
    }

    private async Task<ExternalProcessResult> RunYtDlpAsync(string outputPath)
    {
        var started = _ytDlpRunner.Start(_context, outputPath);
        if (!started.Started || started.Process is null)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, _context.YtDlpLogPath, started.Exception);
        }

        lock (_syncRoot)
        {
            _activeProcess = started.Process;
        }

        if (!_startNotified)
        {
            _startNotified = true;
            _started();
        }

        try
        {
            return await ProcessExecutionHelper
                .WaitForExitWithLoggingAsync(
                    started.Process,
                    "yt-dlp",
                    YtDlpRunner.BuildArguments(_context, outputPath),
                    _context.YtDlpLogPath,
                    _cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested && !IsStopRequested())
        {
            RequestStop(RecordingStopReason.ApplicationExit);
            throw;
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeProcess?.Dispose();
                _activeProcess = null;
            }
        }
    }

    internal static string BuildAttemptTsPath(RecordingJobContext context, int attempt)
    {
        return BuildAttemptSegmentPath(context, attempt);
    }

    internal static string BuildAttemptSegmentPath(RecordingJobContext context, int attempt)
    {
        var directory = Path.GetDirectoryName(context.TsPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(context.TsPath);
        return Path.Combine(directory, $"{fileName}.segment{attempt:000}.mp4");
    }

    internal static string BuildAttemptOutputTemplate(RecordingJobContext context, int attempt)
    {
        var segmentPath = BuildAttemptSegmentPath(context, attempt);
        var directory = Path.GetDirectoryName(segmentPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(segmentPath);
        return Path.Combine(directory, $"{fileName}.%(ext)s");
    }

    internal static bool HasAnyRecordedContent(IReadOnlyList<string> segmentPaths)
    {
        foreach (var segmentPath in segmentPaths)
        {
            if (HasRecordedContent(segmentPath))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsStillLiveAsync()
    {
        if (IsStopRequested())
        {
            return false;
        }

        var result = await _liveDetector.CheckLiveAsync(
            new AlertConfig
            {
                Label = _context.Label,
                Url = _context.WatchUrl
            },
            _cancellationToken).ConfigureAwait(false);

        return result.IsLive && string.Equals(result.VideoId, _context.VideoId, StringComparison.Ordinal);
    }

    private bool IsStopRequested()
    {
        lock (_syncRoot)
        {
            return _stopReason != RecordingStopReason.None;
        }
    }

    private string GetStopReasonText()
    {
        lock (_syncRoot)
        {
            return _stopReason switch
            {
                RecordingStopReason.ManualStop => "manual",
                RecordingStopReason.ApplicationExit => "application_exit",
                _ => "none"
            };
        }
    }

    private void LogFailure(string failureType, ExternalProcessResult result)
    {
        var detail = result.Exception?.Message
            ?? result.LogPath
            ?? string.Empty;
        AppLog.Error(
            $"Recording failed label={_context.Label} videoId={_context.VideoId} failureType={failureType} exitCode={result.ExitCode?.ToString() ?? "(null)"} logPath={result.LogPath ?? "(none)"} detail={detail}",
            result.Exception);
    }

    internal static bool HasRecordedContent(string tsPath)
    {
        try
        {
            return File.Exists(tsPath) && new FileInfo(tsPath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsNonRetriableYtDlpFailure(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
        {
            return false;
        }

        try
        {
            var tail = ReadLogTail(logPath, maxBytes: 16 * 1024);
            return tail.Contains("available to this channel's members", StringComparison.OrdinalIgnoreCase) ||
                tail.Contains("members-only content", StringComparison.OrdinalIgnoreCase) ||
                tail.Contains("Join this channel to get access", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string ReadLogTail(string logPath, int maxBytes)
    {
        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var byteCount = (int)Math.Min(stream.Length, maxBytes);
        var buffer = new byte[byteCount];
        if (byteCount == 0)
        {
            return string.Empty;
        }

        stream.Seek(-byteCount, SeekOrigin.End);
        var read = stream.Read(buffer, 0, byteCount);
        if (read <= 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }
}
