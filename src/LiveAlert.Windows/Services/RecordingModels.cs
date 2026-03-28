using System.Diagnostics;

namespace LiveAlert.Windows.Services;

public enum RecordingJobState
{
    Recording,
    Retrying,
    Finalizing,
    Completed,
    Failed
}

public sealed record RecordingSettings(
    bool Enabled,
    string SaveDirectory,
    int RetentionDays,
    string CookiesPath);

public sealed record RecordingJobStatusSnapshot(
    string VideoId,
    string Label,
    RecordingJobState State)
{
    public string StateText => State switch
    {
        RecordingJobState.Recording => "録画中",
        RecordingJobState.Retrying => "再試行中",
        RecordingJobState.Finalizing => "マージ中",
        _ => string.Empty
    };
}

public sealed record RecordingJobContext(
    string Label,
    string VideoId,
    string WatchUrl,
    string TsPath,
    string Mp4Path,
    string? CookiesPath);

public sealed record ExternalProcessResult(
    bool Started,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    Exception? Exception = null);

public sealed record ExternalProcessStartResult(
    bool Started,
    Process? Process,
    Exception? Exception = null);

public sealed record CommandProbeResult(bool Success, string Detail);

public sealed record RecordingEnvironmentValidationResult(bool Success, string Message);
