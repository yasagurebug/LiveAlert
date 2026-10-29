using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace LiveAlert.Windows.Services;

internal sealed record RecordingMediaInfo(bool HasVideo, bool HasAudio, double Duration)
{
    public bool HasBothStreams => HasVideo && HasAudio;
}

internal static class RecordingMediaInspector
{
    public static RecordingMediaInfo Inspect(string path, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("ffprobe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in new[] { "-v", "quiet", "-show_entries", "stream=codec_type:format=duration", "-of", "json", path })
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
            var json = output.GetAwaiter().GetResult();
            error.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"録画ファイルを解析できません: {path}");
            }
            return Parse(json);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"録画ファイルの解析がタイムアウトしました: {path}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    internal static RecordingMediaInfo Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var video = false;
        var audio = false;
        if (document.RootElement.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                if (!stream.TryGetProperty("codec_type", out var type)) continue;
                video |= type.GetString() == "video";
                audio |= type.GetString() == "audio";
            }
        }

        var duration = 0d;
        if (document.RootElement.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var value))
        {
            double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
        }
        return new RecordingMediaInfo(video, audio, duration);
    }
}
