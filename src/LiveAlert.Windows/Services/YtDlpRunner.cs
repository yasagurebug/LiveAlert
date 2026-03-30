using System.IO;

namespace LiveAlert.Windows.Services;

public sealed class YtDlpRunner
{
    public ExternalProcessStartResult Start(RecordingJobContext context)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.TsPath) ?? ".");
        return ProcessExecutionHelper.Start("yt-dlp", BuildArguments(context));
    }

    internal static string BuildArguments(RecordingJobContext context)
    {
        var cookiesArgument = string.IsNullOrWhiteSpace(context.CookiesPath)
            ? string.Empty
            : $" --cookies {Quote(context.CookiesPath)}";
        return
            $"--no-part --no-progress --hls-use-mpegts -f {Quote("bestvideo*+bestaudio/best")} -o {Quote(context.TsPath)}{cookiesArgument} {Quote(context.WatchUrl)}";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
