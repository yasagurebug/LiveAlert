using System.IO;

namespace LiveAlert.Windows.Services;

public sealed class RetentionCleaner
{
    public int Cleanup(string directory, int retentionDays, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        var threshold = now.LocalDateTime.AddDays(-Math.Max(1, retentionDays));
        var deleted = 0;
        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var lastWriteTime = File.GetLastWriteTime(filePath);
                if (lastWriteTime < threshold)
                {
                    File.Delete(filePath);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Retention cleanup skipped path={filePath} reason={ex.Message}");
            }
        }

        return deleted;
    }
}
