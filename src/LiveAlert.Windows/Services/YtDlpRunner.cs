using System.IO;

namespace LiveAlert.Windows.Services;

public sealed class YtDlpRunner
{
    public ExternalProcessStartResult Start(RecordingJobContext context)
    {
        return Start(context, context.TsPath);
    }

    public ExternalProcessStartResult Start(RecordingJobContext context, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.TsPath) ?? ".");
        return ProcessExecutionHelper.Start("yt-dlp", BuildArguments(context, outputPath));
    }

    internal static string BuildArguments(RecordingJobContext context)
    {
        return BuildArguments(context, context.TsPath);
    }

    internal static string BuildArguments(RecordingJobContext context, string outputPath)
    {
        var cookiesArgument = string.IsNullOrWhiteSpace(context.CookiesPath)
            ? string.Empty
            : $" --cookies {Quote(context.CookiesPath)}";
        return
            $"--live-from-start --no-progress --merge-output-format mp4 -f {Quote("(bv*+ba)/b")} -o {Quote(outputPath)}{cookiesArgument} {Quote(context.WatchUrl)}";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
