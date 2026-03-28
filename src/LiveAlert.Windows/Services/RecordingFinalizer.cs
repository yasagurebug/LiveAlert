namespace LiveAlert.Windows.Services;

public sealed class RecordingFinalizer
{
    public ExternalProcessResult FinalizeToMp4(RecordingJobContext context, CancellationToken cancellationToken)
    {
        var arguments =
            $"-y -i {Quote(context.TsPath)} -c copy {Quote(context.Mp4Path)}";
        return ProcessExecutionHelper.StartAndWait("ffmpeg", arguments, cancellationToken);
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
