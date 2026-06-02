using System.IO;
using System.Text;

namespace LiveAlert.Windows.Services;

public sealed class RecordingFinalizer
{
    public ExternalProcessResult FinalizeToMp4(RecordingJobContext context, CancellationToken cancellationToken)
    {
        return FinalizeToMp4(context, new[] { context.TsPath }, cancellationToken);
    }

    public ExternalProcessResult FinalizeToMp4(
        RecordingJobContext context,
        IReadOnlyList<string> segmentPaths,
        CancellationToken cancellationToken)
    {
        var arguments =
            segmentPaths.Count <= 1
                ? $"-y -i {Quote(segmentPaths[0])} -c copy {Quote(context.Mp4Path)}"
                : BuildConcatArguments(context, segmentPaths);
        var result = ProcessExecutionHelper.StartAndWait("ffmpeg", arguments, context.FfmpegLogPath, cancellationToken);
        DeleteConcatList(context);
        return result;
    }

    private static string BuildConcatArguments(RecordingJobContext context, IReadOnlyList<string> segmentPaths)
    {
        var listPath = GetConcatListPath(context);
        var builder = new StringBuilder();
        foreach (var segmentPath in segmentPaths)
        {
            builder.Append("file '");
            builder.Append(segmentPath.Replace("'", "'\\''"));
            builder.Append('\'');
            builder.AppendLine();
        }

        File.WriteAllText(listPath, builder.ToString(), new UTF8Encoding(false));
        return $"-y -f concat -safe 0 -i {Quote(listPath)} -c copy {Quote(context.Mp4Path)}";
    }

    private static string GetConcatListPath(RecordingJobContext context)
    {
        return Path.ChangeExtension(context.Mp4Path, ".concat.txt");
    }

    private static void DeleteConcatList(RecordingJobContext context)
    {
        try
        {
            var listPath = GetConcatListPath(context);
            if (File.Exists(listPath))
            {
                File.Delete(listPath);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Recording concat list cleanup skipped path={GetConcatListPath(context)} reason={ex.Message}");
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
