using System.Diagnostics;
using System.IO;
using System.Text;

namespace LiveAlert.Windows.Services;

internal static class ProcessExecutionHelper
{
    public static ExternalProcessStartResult Start(string fileName, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                process.Dispose();
                return new ExternalProcessStartResult(false, null);
            }

            return new ExternalProcessStartResult(true, process);
        }
        catch (Exception ex)
        {
            return new ExternalProcessStartResult(false, null, ex);
        }
    }

    public static ExternalProcessResult StartAndWait(string fileName, string arguments, CancellationToken cancellationToken)
    {
        return StartAndWait(fileName, arguments, null, cancellationToken);
    }

    public static ExternalProcessResult StartAndWait(string fileName, string arguments, string? logPath, CancellationToken cancellationToken)
    {
        var started = Start(fileName, arguments);
        if (!started.Started || started.Process is null)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, logPath, started.Exception);
        }

        using var process = started.Process;
        try
        {
            PrepareLogFile(logPath, fileName, arguments);
            AttachLogWriters(process, logPath);
            process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();
            process.WaitForExit();

            return new ExternalProcessResult(true, process.ExitCode, string.Empty, string.Empty, logPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, logPath, ex);
        }
    }

    public static async Task<ExternalProcessResult> WaitForExitWithLoggingAsync(
        Process process,
        string fileName,
        string arguments,
        string? logPath,
        CancellationToken cancellationToken)
    {
        try
        {
            PrepareLogFile(logPath, fileName, arguments);
            AttachLogWriters(process, logPath);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            process.WaitForExit();

            return new ExternalProcessResult(true, process.ExitCode, string.Empty, string.Empty, logPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, logPath, ex);
        }
    }

    private static void PrepareLogFile(string? logPath, string fileName, string arguments)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            logPath,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] command={fileName} arguments={arguments}{Environment.NewLine}",
            new UTF8Encoding(false));
    }

    private static void AttachLogWriters(Process process, string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return;
        }

        var syncRoot = new object();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                AppendLogLine(logPath, syncRoot, "stdout", args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                AppendLogLine(logPath, syncRoot, "stderr", args.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private static void AppendLogLine(string logPath, object syncRoot, string streamName, string line)
    {
        lock (syncRoot)
        {
            File.AppendAllText(
                logPath,
                $"[{streamName}] {line}{Environment.NewLine}",
                new UTF8Encoding(false));
        }
    }
}
