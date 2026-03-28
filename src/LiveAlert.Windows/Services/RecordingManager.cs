using System.IO;
using LiveAlert.Core;

namespace LiveAlert.Windows.Services;

public sealed class RecordingManager
{
    private readonly YtDlpRunner _ytDlpRunner;
    private readonly RecordingFinalizer _finalizer;
    private readonly ILiveDetector _liveDetector;
    private readonly RecordingProcessController _processController;
    private readonly Dictionary<string, RecordingJobStatusSnapshot> _snapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RecordingJob> _jobs = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    public RecordingManager(YtDlpRunner ytDlpRunner, RecordingFinalizer finalizer, ILiveDetector liveDetector)
    {
        _ytDlpRunner = ytDlpRunner;
        _finalizer = finalizer;
        _liveDetector = liveDetector;
        _processController = new RecordingProcessController();
    }

    public event Action<IReadOnlyList<RecordingJobStatusSnapshot>>? StatusesChanged;
    public event Action<string>? RecordingStarted;
    public event Action<string>? RecordingFailed;

    public bool TryStart(AlertEvent alertEvent, RecordingSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.SaveDirectory))
        {
            return false;
        }

        var cookiesPath = ResolveCookiesPath(settings.CookiesPath);
        var context = RecordingPathBuilder.Build(
            settings.SaveDirectory,
            alertEvent.Alert.Label,
            alertEvent.VideoId,
            alertEvent.DetectedAt.LocalDateTime,
            cookiesPath);

        RecordingJob job;
        lock (_syncRoot)
        {
            if (_jobs.ContainsKey(alertEvent.VideoId))
            {
                return false;
            }

            job = new RecordingJob(
                context,
                _ytDlpRunner,
                _finalizer,
                _liveDetector,
                _processController,
                state => UpdateSnapshot(context.VideoId, context.Label, state),
                () =>
                {
                    AppLog.Info(
                        $"Recording started label={context.Label} videoId={context.VideoId} output={context.TsPath} cookiesUsed={!string.IsNullOrWhiteSpace(context.CookiesPath)}");
                    RecordingStarted?.Invoke(context.Label);
                },
                cancellationToken);
            _jobs[alertEvent.VideoId] = job;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await job.RunAsync().ConfigureAwait(false);
                CompleteJob(context.VideoId);
            }
            catch (OperationCanceledException)
            {
                CompleteJob(context.VideoId);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Recording manager observed failure label={context.Label} videoId={context.VideoId}", ex);
                CompleteJob(context.VideoId);
                RecordingFailed?.Invoke(context.Label);
            }
        }, cancellationToken);

        return true;
    }

    public void Stop(string videoId)
    {
        RecordingJob? job;
        lock (_syncRoot)
        {
            _jobs.TryGetValue(videoId, out job);
        }

        job?.RequestStop(RecordingStopReason.ManualStop);
    }

    public void StopAllForApplicationExit()
    {
        List<RecordingJob> jobs;
        lock (_syncRoot)
        {
            jobs = _jobs.Values.ToList();
        }

        foreach (var job in jobs)
        {
            job.RequestStop(RecordingStopReason.ApplicationExit);
        }
    }

    private void UpdateSnapshot(string videoId, string label, RecordingJobState state)
    {
        lock (_syncRoot)
        {
            if (state is RecordingJobState.Completed or RecordingJobState.Failed)
            {
                _snapshots.Remove(videoId);
            }
            else
            {
                _snapshots[videoId] = new RecordingJobStatusSnapshot(videoId, label, state);
            }
        }

        RaiseStatusesChanged();
    }

    private void CompleteJob(string videoId)
    {
        lock (_syncRoot)
        {
            _snapshots.Remove(videoId);
            _jobs.Remove(videoId);
        }

        RaiseStatusesChanged();
    }

    private void RaiseStatusesChanged()
    {
        List<RecordingJobStatusSnapshot> items;
        lock (_syncRoot)
        {
            items = _snapshots.Values
                .OrderBy(item => item.Label, StringComparer.CurrentCulture)
                .ToList();
        }

        StatusesChanged?.Invoke(items);
    }

    private static string? ResolveCookiesPath(string? cookiesPath)
    {
        if (string.IsNullOrWhiteSpace(cookiesPath))
        {
            return null;
        }

        try
        {
            return File.Exists(cookiesPath) ? cookiesPath : null;
        }
        catch
        {
            return null;
        }
    }
}
