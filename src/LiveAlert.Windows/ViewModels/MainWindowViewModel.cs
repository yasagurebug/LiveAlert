using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using LiveAlert.Core;
using LiveAlert.Windows.Infrastructure;

namespace LiveAlert.Windows.ViewModels;

public sealed class MainWindowViewModel : BindableBase, IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly ObservableCollection<AlertEditorViewModel> _alerts = new();
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private AlertEditorViewModel? _selectedAlert;
    private string _statusText = "起動中";
    private string _currentAlertText = "なし";
    private int _pollIntervalSec = 60;
    private int _maxAlarmDurationSec = 30;
    private int _loopIntervalSec = 5;
    private int _bandHeightPx = 220;
    private string _bandPosition = "top";
    private bool _windowsAutoStart;

    public MainWindowViewModel(ConfigManager configManager)
    {
        _configManager = configManager;
        ConfigPath = _configManager.ConfigPath;
        AppDisplayName = BuildAppDisplayName();
        Alerts.CollectionChanged += HandleAlertsChanged;
    }

    public string AppDisplayName { get; }

    public string ConfigPath { get; }

    public ObservableCollection<AlertEditorViewModel> Alerts => _alerts;

    public AlertEditorViewModel? SelectedAlert
    {
        get => _selectedAlert;
        set => SetProperty(ref _selectedAlert, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string CurrentAlertText
    {
        get => _currentAlertText;
        set => SetProperty(ref _currentAlertText, value);
    }

    public bool WindowsAutoStart
    {
        get => _windowsAutoStart;
        set
        {
            if (SetProperty(ref _windowsAutoStart, value))
            {
                QueueSave();
            }
        }
    }

    public int PollIntervalSec
    {
        get => _pollIntervalSec;
        set
        {
            if (SetProperty(ref _pollIntervalSec, value))
            {
                QueueSave();
            }
        }
    }

    public int MaxAlarmDurationSec
    {
        get => _maxAlarmDurationSec;
        set
        {
            if (SetProperty(ref _maxAlarmDurationSec, value))
            {
                QueueSave();
            }
        }
    }

    public int LoopIntervalSec
    {
        get => _loopIntervalSec;
        set
        {
            if (SetProperty(ref _loopIntervalSec, value))
            {
                QueueSave();
            }
        }
    }

    public int BandHeightPx
    {
        get => _bandHeightPx;
        set
        {
            if (SetProperty(ref _bandHeightPx, value))
            {
                QueueSave();
            }
        }
    }

    public string BandPosition
    {
        get => _bandPosition;
        set
        {
            if (SetProperty(ref _bandPosition, value))
            {
                QueueSave();
            }
        }
    }

    public void Load(ConfigRoot config)
    {
        _suppressSave = true;
        try
        {
            Alerts.Clear();
            foreach (var alert in config.Alerts.Where(IsYouTube))
            {
                var item = AlertEditorViewModel.FromConfig(alert);
                AttachAlert(item);
                Alerts.Add(item);
            }

            if (Alerts.Count == 0)
            {
                var item = AlertEditorViewModel.FromConfig(ConfigDefaults.CreateDefault().Alerts[0]);
                AttachAlert(item);
                Alerts.Add(item);
            }

            SelectedAlert = Alerts[0];
            PollIntervalSec = config.Options.PollIntervalSec;
            MaxAlarmDurationSec = config.Options.MaxAlarmDurationSec;
            LoopIntervalSec = config.Options.LoopIntervalSec;
            BandHeightPx = config.Options.BandHeightPx;
            BandPosition = NormalizePosition(config.Options.BandPosition);
            WindowsAutoStart = config.Options.WindowsAutoStart;
        }
        finally
        {
            _suppressSave = false;
        }
    }

    public void AddAlert()
    {
        var alert = AlertEditorViewModel.FromConfig(new AlertConfig
        {
            Service = "youtube",
            Message = "警告　{label} がライブ開始"
        });
        AttachAlert(alert);
        Alerts.Add(alert);
        SelectedAlert = alert;
        QueueSave();
    }

    public void RemoveSelectedAlert()
    {
        if (SelectedAlert is null || Alerts.Count <= 1)
        {
            return;
        }

        SelectedAlert.PropertyChanged -= HandleAlertPropertyChanged;
        var index = Alerts.IndexOf(SelectedAlert);
        Alerts.Remove(SelectedAlert);
        SelectedAlert = Alerts[Math.Clamp(index - 1, 0, Alerts.Count - 1)];
        QueueSave();
    }

    public async Task SaveNowAsync()
    {
        _saveCts?.Cancel();
        await SaveAsync(CancellationToken.None);
    }

    public ConfigRoot BuildConfig()
    {
        return new ConfigRoot
        {
            Alerts = Alerts.Select(alert => alert.ToConfig()).ToList(),
            Options = new AlertOptions
            {
                PollIntervalSec = Math.Clamp(PollIntervalSec, 60, 600),
                MaxAlarmDurationSec = Math.Clamp(MaxAlarmDurationSec, 15, 600),
                LoopIntervalSec = Math.Clamp(LoopIntervalSec, 0, 60),
                BandHeightPx = Math.Clamp(BandHeightPx, 96, 800),
                BandPosition = NormalizePosition(BandPosition),
                HotReload = true,
                NotificationMode = "off",
                DisplayMode = "alarm",
                AudioMode = "alarm",
                DebugMode = false,
                DedupeMinutes = 5,
                WindowsAutoStart = WindowsAutoStart
            }
        };
    }

    public void Dispose()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        foreach (var alert in Alerts)
        {
            alert.PropertyChanged -= HandleAlertPropertyChanged;
        }
    }

    private void HandleAlertsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (AlertEditorViewModel item in e.OldItems)
            {
                item.PropertyChanged -= HandleAlertPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (AlertEditorViewModel item in e.NewItems)
            {
                AttachAlert(item);
            }
        }
    }

    private void AttachAlert(AlertEditorViewModel alert)
    {
        alert.PropertyChanged -= HandleAlertPropertyChanged;
        alert.PropertyChanged += HandleAlertPropertyChanged;
    }

    private void HandleAlertPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        QueueSave();
    }

    private void QueueSave()
    {
        if (_suppressSave)
        {
            return;
        }

        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token).ConfigureAwait(false);
                await SaveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var config = BuildConfig();
        await _configManager.SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsYouTube(AlertConfig alert)
    {
        return string.IsNullOrWhiteSpace(alert.Service) ||
               alert.Service.Equals("youtube", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePosition(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "center" => "center",
            "bottom" => "bottom",
            _ => "top"
        };
    }

    private static string BuildAppDisplayName()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString()
            : informational;

        if (string.IsNullOrWhiteSpace(version))
        {
            return "LiveAlert Windows";
        }

        var normalized = version.Split('+', 2)[0];
        return $"LiveAlert Windows {normalized}";
    }
}
