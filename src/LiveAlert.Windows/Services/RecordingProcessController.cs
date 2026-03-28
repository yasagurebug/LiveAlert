using System.Diagnostics;
using System.Management;

namespace LiveAlert.Windows.Services;

public sealed class RecordingProcessController
{
    public void StopProcessesForRecording(RecordingJobContext context)
    {
        KillTrackedProcess("yt-dlp.exe", context);
        KillTrackedProcess("ffmpeg.exe", context);
    }

    private static void KillTrackedProcess(string processName, RecordingJobContext context)
    {
        foreach (var processInfo in FindProcesses(processName))
        {
            if (!MatchesRecording(processInfo.CommandLine, context))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(processInfo.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Recording process kill skipped name={processName} pid={processInfo.ProcessId} reason={ex.Message}");
            }
        }
    }

    private static bool MatchesRecording(string? commandLine, RecordingJobContext context)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        return commandLine.Contains(context.TsPath, StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains(context.WatchUrl, StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains($"file:{context.TsPath}", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ProcessInfo> FindProcesses(string processName)
    {
        var escaped = processName.Replace("\\", "\\\\").Replace("'", "\\'");
        using var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = '{escaped}'");
        using var results = searcher.Get();
        var items = new List<ProcessInfo>();
        foreach (ManagementObject result in results)
        {
            var processId = Convert.ToInt32(result["ProcessId"]);
            var commandLine = result["CommandLine"]?.ToString();
            items.Add(new ProcessInfo(processId, commandLine));
        }

        return items;
    }

    private sealed record ProcessInfo(int ProcessId, string? CommandLine);
}
