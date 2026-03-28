using System.Diagnostics;
using System.Text;

namespace LiveAlert.Windows.Services;

public interface IExternalCommandProbe
{
    Task<CommandProbeResult> ProbeAsync(string fileName, string arguments, CancellationToken cancellationToken);
}

public sealed class ExternalCommandProbe : IExternalCommandProbe
{
    public async Task<CommandProbeResult> ProbeAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            if (!process.Start())
            {
                return new CommandProbeResult(false, $"{fileName} の起動に失敗しました。");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                return new CommandProbeResult(true, string.Empty);
            }

            var detail = BuildDetail(stdout, stderr);
            return new CommandProbeResult(false, $"{fileName} が終了コード {process.ExitCode} を返しました。{detail}".Trim());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CommandProbeResult(false, $"{fileName} の確認に失敗しました: {ex.Message}");
        }
    }

    private static string BuildDetail(string stdout, string stderr)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= 160 ? text : text[..160];
    }
}
