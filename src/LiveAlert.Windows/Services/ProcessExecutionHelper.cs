using System.Diagnostics;
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
        var started = Start(fileName, arguments);
        if (!started.Started || started.Process is null)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, started.Exception);
        }

        using var process = started.Process;
        try
        {
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    stdoutBuilder.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    stderrBuilder.AppendLine(args.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExitAsync(cancellationToken).GetAwaiter().GetResult();

            return new ExternalProcessResult(true, process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ExternalProcessResult(false, null, string.Empty, string.Empty, ex);
        }
    }
}
