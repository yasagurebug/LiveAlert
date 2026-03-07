using System.Drawing;
using System.Windows.Forms;

namespace LiveAlert.Windows.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _stopAlertMenuItem;

    public event Action? OpenSettingsRequested;
    public event Action? StopAlertRequested;
    public event Action? TestAlertRequested;
    public event Action? OpenConfigFolderRequested;
    public event Action? ShowAboutRequested;
    public event Action? ShowLicensesRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _stopAlertMenuItem = new ToolStripMenuItem("アラーム停止");
        _stopAlertMenuItem.Enabled = false;

        var settingsMenuItem = new ToolStripMenuItem("設定を開く");
        var testAlertMenuItem = new ToolStripMenuItem("テスト発報");
        var openConfigFolderMenuItem = new ToolStripMenuItem("設定フォルダを開く");
        var aboutMenuItem = new ToolStripMenuItem("このプログラムについて");
        var licensesMenuItem = new ToolStripMenuItem("外部ライセンス");
        var exitMenuItem = new ToolStripMenuItem("終了");

        settingsMenuItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        _stopAlertMenuItem.Click += (_, _) => StopAlertRequested?.Invoke();
        testAlertMenuItem.Click += (_, _) => TestAlertRequested?.Invoke();
        openConfigFolderMenuItem.Click += (_, _) => OpenConfigFolderRequested?.Invoke();
        aboutMenuItem.Click += (_, _) => ShowAboutRequested?.Invoke();
        licensesMenuItem.Click += (_, _) => ShowLicensesRequested?.Invoke();
        exitMenuItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.Add(settingsMenuItem);
        menu.Items.Add(_stopAlertMenuItem);
        menu.Items.Add(testAlertMenuItem);
        menu.Items.Add(openConfigFolderMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(aboutMenuItem);
        menu.Items.Add(licensesMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "LiveAlert",
            Visible = true,
            Icon = ResolveAppIcon(),
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke();
    }

    public void UpdateAlertState(bool hasActiveAlert)
    {
        _stopAlertMenuItem.Enabled = hasActiveAlert;
    }

    public void UpdateStatusText(string text)
    {
        _notifyIcon.Text = Truncate(text, 63);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "LiveAlert";
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Icon ResolveAppIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var icon = Icon.ExtractAssociatedIcon(processPath);
            if (icon is not null)
            {
                return icon;
            }
        }

        return SystemIcons.Warning;
    }
}
