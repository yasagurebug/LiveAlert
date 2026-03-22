using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using LiveAlert.Core;
using LiveAlert.Windows.ViewModels;
using LiveAlert.Windows.Views;

namespace LiveAlert.Windows.Services;

public sealed class AppController : IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly AlertMonitor _monitor;
    private readonly AlertQueue _queue = new();
    private readonly TrayIconService _trayIcon;
    private readonly AlertAudioPlayer _audioPlayer;
    private readonly SessionStateMonitor _sessionMonitor;
    private readonly StartupRegistrationService _startupRegistration;
    private readonly NicoEasterEggController _nicoEasterEgg;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _shutdownCts = new();
    private CancellationTokenSource? _monitoringCts;
    private CancellationTokenSource? _currentAlertCts;
    private OverlayWindow? _overlayWindow;
    private MainWindow? _mainWindow;
    private AlertQueueItem? _currentItem;
    private volatile bool _monitorLoopObservedSuccessfulCycle;

    public AppController()
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);

        _configManager = new ConfigManager(AppPaths.ConfigPath);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _monitor = new AlertMonitor(_configManager, new YouTubeLiveDetector(_httpClient));
        _trayIcon = new TrayIconService();
        _audioPlayer = new AlertAudioPlayer();
        _sessionMonitor = new SessionStateMonitor();
        _startupRegistration = new StartupRegistrationService();
        _nicoEasterEgg = new NicoEasterEggController();
        ViewModel = new MainWindowViewModel(_configManager);
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;

        _monitor.AlertDetected += HandleAlertDetected;
        _monitor.AlertEnded += HandleAlertEnded;
        _monitor.MonitoringSummaryUpdated += HandleMonitoringSummaryUpdated;
        _monitor.MonitoringFailureDetected += HandleMonitoringFailureDetected;
        _monitor.MonitoringDebug += message => AppLog.Info(message);

        _trayIcon.OpenSettingsRequested += ShowSettingsWindow;
        _trayIcon.StopAlertRequested += () => StopCurrentAlert(false);
        _trayIcon.TestAlertRequested += TriggerTestAlert;
        _trayIcon.OpenConfigFolderRequested += OpenConfigFolder;
        _trayIcon.ShowAboutRequested += ShowAboutWindow;
        _trayIcon.ShowLicensesRequested += ShowLicensesWindow;
        _trayIcon.ExitRequested += ExitApplication;

        _sessionMonitor.LockStateChanged += HandleLockStateChanged;
        _nicoEasterEgg.SpriteClicked += HandleOverlayClicked;
    }

    public MainWindowViewModel ViewModel { get; }

    public bool IsExiting { get; private set; }

    public bool StartHiddenOnLaunch => ViewModel.WindowsAutoStart;

    public async Task InitializeAsync()
    {
        AppLog.Info("Windows app initialize");
        await _configManager.LoadAsync(_shutdownCts.Token);
        ViewModel.Load(_configManager.Current);
        _startupRegistration.SetEnabled(ViewModel.WindowsAutoStart);
        StartMonitoring();
    }

    public void AttachWindow(MainWindow window)
    {
        _mainWindow = window;
    }

    public void ShowSettingsWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow is null)
            {
                return;
            }

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _mainWindow.WindowState = System.Windows.WindowState.Normal;
            }

            _mainWindow.Activate();
        });
    }

    public void StopCurrentAlert(bool openTarget)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var targetUrl = openTarget ? GetCurrentTargetUrl() : null;

            _currentAlertCts?.Cancel();
            _currentAlertCts?.Dispose();
            _currentAlertCts = null;

            _audioPlayer.Stop();
            _overlayWindow?.Hide();
            _nicoEasterEgg.Stop();
            _currentItem = null;
            ViewModel.CurrentAlertText = "なし";
            _trayIcon.UpdateAlertState(false);

            if (!string.IsNullOrWhiteSpace(targetUrl))
            {
                OpenUrl(targetUrl);
            }

            ProcessQueue();
        });
    }

    public void TriggerTestAlert()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var alert = ViewModel.SelectedAlert?.ToConfig() ?? ViewModel.Alerts.First().ToConfig();
            var videoId = $"test:{Guid.NewGuid():N}";
            _queue.Enqueue(new AlertQueueItem(new AlertEvent(alert, 0, videoId, DateTimeOffset.UtcNow), DateTimeOffset.UtcNow));
            ProcessQueue();
        });
    }

    public void OpenConfigFolder()
    {
        var configPath = AppPaths.ConfigPath;
        if (File.Exists(configPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{configPath}\"")
            {
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.AppDataDirectory)
        {
            UseShellExecute = true
        });
    }

    public async Task FlushConfigAsync()
    {
        await ViewModel.SaveNowAsync();
    }

    public void Dispose()
    {
        StopMonitoring();
        _shutdownCts.Cancel();
        _overlayWindow?.Close();
        _nicoEasterEgg.Dispose();
        _trayIcon.Dispose();
        _sessionMonitor.Dispose();
        _audioPlayer.Dispose();
        _httpClient.Dispose();
        _shutdownCts.Dispose();
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.Dispose();
    }

    private void StartMonitoring()
    {
        if (_monitoringCts is not null)
        {
            return;
        }

        ViewModel.StatusText = "監視中";
        _trayIcon.UpdateStatusText("LiveAlert");

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
        var token = _monitoringCts.Token;
        _ = Task.Run(() => RunMonitoringSupervisorAsync(token), token);
    }

    private async Task RunMonitoringSupervisorAsync(CancellationToken token)
    {
        var restartDelaySeconds = 5;
        while (!token.IsCancellationRequested)
        {
            _monitorLoopObservedSuccessfulCycle = false;

            try
            {
                AppLog.Info($"Monitoring supervisor starting monitor loop restartDelaySec={restartDelaySeconds}");
                await _monitor.RunAsync(token).ConfigureAwait(false);

                if (token.IsCancellationRequested)
                {
                    break;
                }

                AppLog.Error("Monitoring loop exited unexpectedly without cancellation.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex)
            {
                AppLog.Error($"Monitoring loop canceled unexpectedly. {FormatExceptionSummary(ex)}", ex);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Monitoring loop failed unexpectedly. {FormatExceptionSummary(ex)}", ex);
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            if (_monitorLoopObservedSuccessfulCycle)
            {
                restartDelaySeconds = 5;
            }

            AppLog.Warn($"Monitoring supervisor restarting after {restartDelaySeconds} seconds.");
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(restartDelaySeconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }

            if (!_monitorLoopObservedSuccessfulCycle)
            {
                restartDelaySeconds = restartDelaySeconds <= int.MaxValue / 2
                    ? restartDelaySeconds * 2
                    : int.MaxValue;
            }
        }

        AppLog.Info("Monitoring supervisor stopped.");
    }

    private static string FormatExceptionSummary(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add($"Type={current.GetType().FullName} Message={current.Message}");
        }

        return string.Join(" | Inner=", parts);
    }

    private void StopMonitoring()
    {
        if (_monitoringCts is null)
        {
            return;
        }

        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
    }

    private void HandleAlertDetected(AlertEvent alertEvent)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _queue.Enqueue(new AlertQueueItem(alertEvent, alertEvent.DetectedAt));
            AppLog.Info($"Alert detected label={SafeLabel(alertEvent.Alert.Label)} videoId={alertEvent.VideoId}");
            ProcessQueue();
        });
    }

    private void HandleAlertEnded(string videoId)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _queue.RemoveByVideoId(videoId);
            if (_currentItem?.VideoId == videoId)
            {
                StopCurrentAlert(false);
            }
        });
    }

    private void HandleMonitoringSummaryUpdated(MonitoringSummary summary)
    {
        _monitorLoopObservedSuccessfulCycle = true;
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm");
            var text = summary.AnyError
                ? $"{timestamp} 監視に失敗しました"
                : summary.LiveLabels.Count > 0
                    ? $"{timestamp} 監視: {string.Join(", ", summary.LiveLabels)} でLIVE検知"
                    : $"{timestamp} 監視: LIVEなし";
            ViewModel.StatusText = text;
            _trayIcon.UpdateStatusText(text);
        });
    }

    private void HandleMonitoringFailureDetected(MonitoringFailure failure)
    {
        AppLog.Warn($"Monitoring failure label={SafeLabel(failure.Label)} url={failure.Url} reason={failure.Reason}");
    }

    private void HandleLockStateChanged(bool isLocked)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (isLocked)
            {
                _overlayWindow?.Hide();
                _nicoEasterEgg.Stop();
                return;
            }

            if (_currentItem is not null)
            {
                ShowOverlay(_currentItem);
            }
        });
    }

    private void ProcessQueue()
    {
        if (_currentItem is not null)
        {
            return;
        }

        var next = _queue.DequeueNext();
        if (next is null)
        {
            return;
        }

        _currentItem = next;
        ViewModel.CurrentAlertText = SafeLabel(next.Alert.Label);
        _trayIcon.UpdateAlertState(true);

        _audioPlayer.Start(next.Alert, ViewModel.BuildConfig().Options);

        if (!_sessionMonitor.IsLocked)
        {
            ShowOverlay(next);
        }

        ScheduleAutoStop();
    }

    private void ShowOverlay(AlertQueueItem item)
    {
        _overlayWindow ??= new OverlayWindow();
        _overlayWindow.BandClicked -= HandleOverlayClicked;
        _overlayWindow.BandClicked += HandleOverlayClicked;
        _overlayWindow.Apply(item.Alert, ViewModel.BuildConfig().Options);
        _nicoEasterEgg.Start(item.Alert);

        if (!_overlayWindow.IsVisible)
        {
            _overlayWindow.Show();
        }
    }

    private void HandleOverlayClicked()
    {
        StopCurrentAlert(true);
    }

    private void ScheduleAutoStop()
    {
        _currentAlertCts?.Cancel();
        _currentAlertCts?.Dispose();
        _currentAlertCts = new CancellationTokenSource();
        var token = _currentAlertCts.Token;
        var duration = TimeSpan.FromSeconds(Math.Max(1, ViewModel.BuildConfig().Options.MaxAlarmDurationSec));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(duration, token).ConfigureAwait(false);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StopCurrentAlert(false));
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private string? GetCurrentTargetUrl()
    {
        if (_currentItem is null || IsSampleAlert(_currentItem.Alert.Label))
        {
            return null;
        }

        return $"https://www.youtube.com/watch?v={_currentItem.VideoId}";
    }

    private static string SafeLabel(string? label)
    {
        return string.IsNullOrWhiteSpace(label) ? "(no label)" : label.Trim();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Warn($"OpenUrl failed url={url} error={ex.Message}");
        }
    }

    private void ShowAboutWindow()
    {
        ShowTextDocument("このプログラムについて", AppAssets.AboutTextUri);
    }

    private void ShowLicensesWindow()
    {
        ShowTextDocument("外部ライセンス", AppAssets.LicenseTextUri);
    }

    private void ShowTextDocument(string title, Uri resourceUri)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                var content = AppAssets.ReadText(resourceUri);
                var window = new TextViewerWindow(title, content);
                if (_mainWindow is not null && _mainWindow.IsVisible)
                {
                    window.Owner = _mainWindow;
                }

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    _mainWindow,
                    ex.Message,
                    title,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        });
    }

    private static bool IsSampleAlert(string? label)
    {
        return string.Equals(label?.Trim(), "SAMPLE", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(MainWindowViewModel.WindowsAutoStart), StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _startupRegistration.SetEnabled(ViewModel.WindowsAutoStart);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Startup registration failed: {ex.Message}");
        }
    }

    private void ExitApplication()
    {
        IsExiting = true;
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await FlushConfigAsync();
            }
            catch (Exception ex)
            {
                AppLog.Error("Failed to flush config", ex);
            }

            System.Windows.Application.Current.Shutdown();
        });
    }
}
