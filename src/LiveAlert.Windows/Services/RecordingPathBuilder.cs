using System.IO;
using System.Text;

namespace LiveAlert.Windows.Services;

public static class RecordingPathBuilder
{
    public static RecordingJobContext Build(string saveDirectory, string label, string videoId, DateTime localNow, string? cookiesPath)
    {
        var baseName = $"{localNow:yyyyMMdd_HHmmss}_{SanitizeFileNamePart(label)}_{SanitizeFileNamePart(videoId)}";
        return new RecordingJobContext(
            string.IsNullOrWhiteSpace(label) ? "(no label)" : label.Trim(),
            videoId,
            $"https://www.youtube.com/watch?v={videoId}",
            Path.Combine(saveDirectory, $"{baseName}.ts"),
            Path.Combine(saveDirectory, $"{baseName}.mp4"),
            cookiesPath);
    }

    public static string SanitizeFileNamePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(IsInvalidFileNameChar(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().TrimEnd(' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }

    private static bool IsInvalidFileNameChar(char value)
    {
        if (char.IsControl(value))
        {
            return true;
        }

        return value is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|';
    }
}
