using LiveAlert.Windows.Infrastructure;

namespace LiveAlert.Windows.ViewModels;

public sealed class RecordingJobStatusViewModel : BindableBase
{
    private string _videoId = string.Empty;
    private string _label = string.Empty;
    private string _stateText = string.Empty;

    public string VideoId
    {
        get => _videoId;
        set => SetProperty(ref _videoId, value);
    }

    public string Label
    {
        get => _label;
        set
        {
            if (SetProperty(ref _label, value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string StateText
    {
        get => _stateText;
        set
        {
            if (SetProperty(ref _stateText, value))
            {
                RaisePropertyChanged(nameof(DisplayText));
            }
        }
    }

    public string DisplayText => $"{Label}：{StateText}";
}
