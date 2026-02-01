using System.Text.Json;

namespace LiveAlert.Core;

public sealed class ConfigManager
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ConfigPath { get; }

    public ConfigRoot Current { get; private set; } = ConfigDefaults.CreateDefault();

    public event Action<ConfigRoot>? ConfigReloaded;

    public ConfigManager(string configPath)
    {
        ConfigPath = configPath;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
            await SaveAsync(Current, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var stream = File.OpenRead(ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<ConfigRoot>(stream, _jsonOptions, cancellationToken)
            .ConfigureAwait(false);

        Current = config ?? ConfigDefaults.CreateDefault();
        if (Current.Options.DedupeMinutes <= 0)
        {
            Current.Options.DedupeMinutes = Math.Clamp(Current.LegacyDedupeMinutes <= 0 ? 5 : Current.LegacyDedupeMinutes, 1, 30);
        }
        Current.LegacyDedupeMinutes = 0;
        ConfigReloaded?.Invoke(Current);
    }

    public async Task SaveAsync(ConfigRoot config, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath) ?? ".");
        config.LegacyDedupeMinutes = 0;
        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, _jsonOptions, cancellationToken).ConfigureAwait(false);
        Current = config;
    }
}
