using LiveAlert.Core;
using LiveAlert.Windows.Infrastructure;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace LiveAlert.Windows.ViewModels;

public sealed class AlertEditorViewModel : BindableBase
{
    private const string DefaultMessage = "警告　{label} がライブ開始";

    private string _url = string.Empty;
    private string _label = string.Empty;
    private string _voice = string.Empty;
    private double _voiceVolume = 100;
    private string _bgm = string.Empty;
    private double _bgmVolume = 50;
    private string _message = DefaultMessage;
    private string _backgroundColor = "#FF0000";
    private string _textColor = "#000000";

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string Label
    {
        get => _label;
        set
        {
            if (SetProperty(ref _label, value))
            {
                RaisePropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public string Voice
    {
        get => _voice;
        set => SetProperty(ref _voice, value);
    }

    public double VoiceVolume
    {
        get => _voiceVolume;
        set => SetProperty(ref _voiceVolume, value);
    }

    public string Bgm
    {
        get => _bgm;
        set => SetProperty(ref _bgm, value);
    }

    public double BgmVolume
    {
        get => _bgmVolume;
        set => SetProperty(ref _bgmVolume, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (SetProperty(ref _backgroundColor, value))
            {
                RaisePropertyChanged(nameof(BackgroundPreviewBrush));
                RaisePropertyChanged(nameof(BackgroundPreviewTextBrush));
            }
        }
    }

    public string TextColor
    {
        get => _textColor;
        set
        {
            if (SetProperty(ref _textColor, value))
            {
                RaisePropertyChanged(nameof(TextPreviewBrush));
                RaisePropertyChanged(nameof(TextPreviewTextBrush));
            }
        }
    }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? "(no label)" : Label;

    public MediaBrush BackgroundPreviewBrush => CreateBrush(BackgroundColor, "#FF0000");

    public MediaBrush BackgroundPreviewTextBrush => CreateContrastBrush(BackgroundColor, "#FF0000");

    public MediaBrush TextPreviewBrush => CreateBrush(TextColor, "#000000");

    public MediaBrush TextPreviewTextBrush => CreateContrastBrush(TextColor, "#000000");

    public static AlertEditorViewModel FromConfig(AlertConfig config)
    {
        return new AlertEditorViewModel
        {
            Url = config.Url ?? string.Empty,
            Label = config.Label ?? string.Empty,
            Voice = config.Voice ?? string.Empty,
            VoiceVolume = config.VoiceVolume,
            Bgm = config.Bgm ?? string.Empty,
            BgmVolume = config.BgmVolume,
            Message = string.IsNullOrWhiteSpace(config.Message) ? DefaultMessage : config.Message,
            BackgroundColor = string.IsNullOrWhiteSpace(config.Colors.Background) ? "#FF0000" : config.Colors.Background,
            TextColor = string.IsNullOrWhiteSpace(config.Colors.Text) ? "#000000" : config.Colors.Text
        };
    }

    public AlertConfig ToConfig()
    {
        return new AlertConfig
        {
            Service = "youtube",
            Url = Url.Trim(),
            Label = Label.Trim(),
            Voice = Voice.Trim(),
            VoiceVolume = Math.Clamp(VoiceVolume, 0, 100),
            Bgm = Bgm.Trim(),
            BgmVolume = Math.Clamp(BgmVolume, 0, 100),
            Message = string.IsNullOrWhiteSpace(Message) ? DefaultMessage : Message.Trim(),
            Colors = new AlertColors
            {
                Background = NormalizeColor(BackgroundColor, "#FF0000"),
                Text = NormalizeColor(TextColor, "#000000")
            }
        };
    }

    private static string NormalizeColor(string value, string fallback)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return fallback;
        }

        return trimmed.StartsWith('#') ? trimmed.ToUpperInvariant() : $"#{trimmed.ToUpperInvariant()}";
    }

    private static MediaBrush CreateBrush(string value, string fallback)
    {
        var color = ParseColor(value, fallback);
        var brush = new MediaSolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static MediaBrush CreateContrastBrush(string value, string fallback)
    {
        var color = ParseColor(value, fallback);
        var brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000d;
        return brightness >= 140 ? MediaBrushes.Black : MediaBrushes.White;
    }

    private static MediaColor ParseColor(string value, string fallback)
    {
        var normalized = NormalizeColor(value, fallback);
        try
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(normalized);
        }
        catch
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(fallback);
        }
    }
}
