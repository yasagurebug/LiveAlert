using LiveAlert.Windows.Services;

namespace LiveAlert.Windows;

public partial class App : System.Windows.Application
{
    private AppController? _controller;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        _controller = new AppController();
        await _controller.InitializeAsync();

        var window = new MainWindow(_controller);
        MainWindow = window;
        _controller.AttachWindow(window);
        if (!_controller.StartHiddenOnLaunch)
        {
            window.Show();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _controller?.Dispose();
        _controller = null;
        base.OnExit(e);
    }
}
