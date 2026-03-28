using System.IO;

namespace LiveAlert.Windows.Services;

public static class AppPaths
{
    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveAlert");

    public static string ConfigPath => Path.Combine(AppDataDirectory, "config.json");

    public static string LogPath => Path.Combine(AppDataDirectory, "livealert.log");

    public static string CookiesPath => Path.Combine(AppDataDirectory, "cookies.txt");
}
