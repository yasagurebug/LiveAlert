using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace LiveAlert.Windows.Services;

public sealed class RecordingFinalizer
{
    public bool PrepareSegment(RecordingJobContext context, string segmentPath, CancellationToken cancellationToken)
    {
        if (File.Exists(segmentPath))
        {
            if (RecordingJob.HasRecordedContent(segmentPath) &&
                RecordingMediaInspector.Inspect(segmentPath, cancellationToken).HasBothStreams)
                return true;
            throw new InvalidDataException($"録画セグメントの映像または音声が不足しています: {segmentPath}");
        }

        var candidates = GetUnmergedMediaPaths(segmentPath);
        if (candidates.Count == 0)
        {
            return false;
        }

        var media = candidates.Select(path => (Path: path, Info: RecordingMediaInspector.Inspect(path, cancellationToken))).ToList();
        var video = media.Where(item => item.Info.HasVideo).ToList();
        var audio = media.Where(item => item.Info.HasAudio).ToList();
        if (video.Count != 1 || audio.Count != 1)
        {
            throw new InvalidDataException($"未結合録画の映像・音声を一意に特定できません: {segmentPath}");
        }

        var temporaryPath = Path.ChangeExtension(segmentPath, ".recovering.mp4");
        var sameInput = video[0].Path == audio[0].Path;
        var inputs = $"-i {Quote(video[0].Path)}" + (sameInput ? "" : $" -i {Quote(audio[0].Path)}");
        var arguments = $"-n {inputs} -map 0:v:0 -map {(sameInput ? 0 : 1)}:a:0 -c copy -movflags +faststart {Quote(temporaryPath)}";
        var result = ProcessExecutionHelper.StartAndWait("ffmpeg", arguments, context.FfmpegLogPath, cancellationToken);
        if (!result.Started || result.ExitCode != 0 || !IsCompleteMp4(temporaryPath, cancellationToken))
        {
            throw new InvalidDataException($"未結合録画の修復に失敗しました。元ファイルを保持します: {segmentPath}", result.Exception);
        }

        File.Move(temporaryPath, segmentPath);
        return true;
    }

    internal static IReadOnlyList<string> GetUnmergedMediaPaths(string segmentPath)
    {
        var directory = Path.GetDirectoryName(segmentPath) ?? ".";
        var prefix = Path.GetFileNameWithoutExtension(segmentPath);
        var pattern = new Regex("^" + Regex.Escape(prefix) + @"\.f[^.]+\.(?:mp4|webm|m4a)(?:\.part)?$", RegexOptions.IgnoreCase);
        return Directory.EnumerateFiles(directory)
            .Where(path => pattern.IsMatch(Path.GetFileName(path)) && RecordingJob.HasRecordedContent(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsCompleteMp4(string path, CancellationToken cancellationToken)
    {
        if (!RecordingJob.HasRecordedContent(path)) return false;
        var info = RecordingMediaInspector.Inspect(path, cancellationToken);
        return info.HasBothStreams && double.IsFinite(info.Duration) && info.Duration > 0;
    }

    public ExternalProcessResult FinalizeToMp4(RecordingJobContext context, CancellationToken cancellationToken)
    {
        return FinalizeToMp4(context, new[] { context.TsPath }, cancellationToken);
    }

    public ExternalProcessResult FinalizeToMp4(
        RecordingJobContext context,
        IReadOnlyList<string> segmentPaths,
        CancellationToken cancellationToken)
    {
        var temporaryPath = Path.ChangeExtension(context.Mp4Path, ".finalizing.mp4");
        var arguments =
            segmentPaths.Count <= 1
                ? $"-n -i {Quote(segmentPaths[0])} -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart {Quote(temporaryPath)}"
                : BuildConcatArguments(context, segmentPaths, temporaryPath);
        var result = ProcessExecutionHelper.StartAndWait("ffmpeg", arguments, context.FfmpegLogPath, cancellationToken);
        DeleteConcatList(context);
        if (result.Started && result.ExitCode == 0)
        {
            if (!IsCompleteMp4(temporaryPath, cancellationToken))
                return result with { ExitCode = -1, Exception = new InvalidDataException("完成録画の映像・音声または再生時間を確認できません。元ファイルを保持します。") };
            File.Move(temporaryPath, context.Mp4Path);
        }
        return result;
    }

    private static string BuildConcatArguments(RecordingJobContext context, IReadOnlyList<string> segmentPaths, string outputPath)
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
        return $"-n -f concat -safe 0 -i {Quote(listPath)} -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart {Quote(outputPath)}";
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
