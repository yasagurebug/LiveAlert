using System.ComponentModel;
using System.Windows;
using LiveAlert.Windows.Services;
using Forms = System.Windows.Forms;

namespace LiveAlert.Windows;

public partial class MainWindow : Window
{
    private readonly AppController _controller;

    public MainWindow(AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        DataContext = controller.ViewModel;
    }

    private void HandleAddAlertClick(object sender, RoutedEventArgs e)
    {
        _controller.ViewModel.AddAlert();
    }

    private void HandleRemoveAlertClick(object sender, RoutedEventArgs e)
    {
        _controller.ViewModel.RemoveSelectedAlert();
    }

    private async void HandleBrowseVoiceClick(object sender, RoutedEventArgs e)
    {
        var file = PickAudioFile();
        if (file is null || _controller.ViewModel.SelectedAlert is null)
        {
            return;
        }

        _controller.ViewModel.SelectedAlert.Voice = file;
        await _controller.FlushConfigAsync();
    }

    private async void HandleBrowseBgmClick(object sender, RoutedEventArgs e)
    {
        var file = PickAudioFile();
        if (file is null || _controller.ViewModel.SelectedAlert is null)
        {
            return;
        }

        _controller.ViewModel.SelectedAlert.Bgm = file;
        await _controller.FlushConfigAsync();
    }

    private async void HandlePickBackgroundColorClick(object sender, RoutedEventArgs e)
    {
        if (_controller.ViewModel.SelectedAlert is null)
        {
            return;
        }

        var selected = PickColor(_controller.ViewModel.SelectedAlert.BackgroundColor);
        if (selected is null)
        {
            return;
        }

        _controller.ViewModel.SelectedAlert.BackgroundColor = selected;
        await _controller.FlushConfigAsync();
    }

    private async void HandlePickTextColorClick(object sender, RoutedEventArgs e)
    {
        if (_controller.ViewModel.SelectedAlert is null)
        {
            return;
        }

        var selected = PickColor(_controller.ViewModel.SelectedAlert.TextColor);
        if (selected is null)
        {
            return;
        }

        _controller.ViewModel.SelectedAlert.TextColor = selected;
        await _controller.FlushConfigAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_controller.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private static string? PickAudioFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio Files|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All Files|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PickColor(string currentHex)
    {
        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };

        if (TryParseColor(currentHex, out var color))
        {
            dialog.Color = color;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK
            ? $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"
            : null;
    }

    private static bool TryParseColor(string value, out System.Drawing.Color color)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            color = System.Drawing.Color.Empty;
            return false;
        }

        if (!normalized.StartsWith('#'))
        {
            normalized = $"#{normalized}";
        }

        try
        {
            color = System.Drawing.ColorTranslator.FromHtml(normalized);
            return true;
        }
        catch
        {
            color = System.Drawing.Color.Empty;
            return false;
        }
    }
}
