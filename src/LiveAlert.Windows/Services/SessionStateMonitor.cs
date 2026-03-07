using Microsoft.Win32;

namespace LiveAlert.Windows.Services;

public sealed class SessionStateMonitor : IDisposable
{
    public event Action<bool>? LockStateChanged;

    public bool IsLocked { get; private set; }

    public SessionStateMonitor()
    {
        SystemEvents.SessionSwitch += HandleSessionSwitch;
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= HandleSessionSwitch;
    }

    private void HandleSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                IsLocked = true;
                LockStateChanged?.Invoke(true);
                break;
            case SessionSwitchReason.SessionUnlock:
                IsLocked = false;
                LockStateChanged?.Invoke(false);
                break;
        }
    }
}
