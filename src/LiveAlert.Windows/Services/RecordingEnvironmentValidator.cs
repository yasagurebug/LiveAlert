using System.IO;

namespace LiveAlert.Windows.Services;

public sealed class RecordingEnvironmentValidator
{
    private readonly IExternalCommandProbe _commandProbe;

    public RecordingEnvironmentValidator(IExternalCommandProbe commandProbe)
    {
        _commandProbe = commandProbe;
    }

    public async Task<RecordingEnvironmentValidationResult> ValidateAsync(string? saveDirectory, CancellationToken cancellationToken)
    {
        var normalizedDirectory = saveDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDirectory))
        {
            return new RecordingEnvironmentValidationResult(false, "保存先フォルダを指定してください。");
        }

        if (!Directory.Exists(normalizedDirectory))
        {
            return new RecordingEnvironmentValidationResult(false, "保存先フォルダが存在しません。");
        }

        var ytDlp = await _commandProbe.ProbeAsync("yt-dlp", "--version", cancellationToken).ConfigureAwait(false);
        if (!ytDlp.Success)
        {
            return new RecordingEnvironmentValidationResult(false, ytDlp.Detail);
        }

        var ffmpeg = await _commandProbe.ProbeAsync("ffmpeg", "-version", cancellationToken).ConfigureAwait(false);
        if (!ffmpeg.Success)
        {
            return new RecordingEnvironmentValidationResult(false, ffmpeg.Detail);
        }

        return new RecordingEnvironmentValidationResult(true, string.Empty);
    }
}
