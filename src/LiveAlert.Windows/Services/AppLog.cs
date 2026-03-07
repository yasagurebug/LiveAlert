using System.Text;
using System.IO;

namespace LiveAlert.Windows.Services;

public static class AppLog
{
    private static readonly object SyncRoot = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        var body = exception is null ? message : $"{message} :: {exception}";
        Write("ERROR", body);
    }

    private static void Write(string level, string message)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(AppPaths.LogPath, line, new UTF8Encoding(false));
        }
    }
}
