using System.Windows.Media;
using LiveAlert.Core;
using System.Windows.Threading;
using System.IO;

namespace LiveAlert.Windows.Services;

public sealed class AlertAudioPlayer : IDisposable
{
    private readonly LoopingTrack _voiceTrack = new();
    private readonly LoopingTrack _bgmTrack = new();

    public void Start(AlertConfig alert, AlertOptions options)
    {
        Stop();

        var voiceSource = ResolveSource(alert.Voice, AppAssets.DefaultVoiceUri);
        var bgmSource = ResolveSource(alert.Bgm, AppAssets.DefaultBgmUri);

        _voiceTrack.Start(voiceSource, alert.VoiceVolume, options.LoopIntervalSec);
        _bgmTrack.Start(bgmSource, alert.BgmVolume, options.LoopIntervalSec);
    }

    public void Stop()
    {
        _voiceTrack.Stop();
        _bgmTrack.Stop();
    }

    public void Dispose()
    {
        Stop();
    }

    private static Uri ResolveSource(string? configuredPath, Uri fallbackUri)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return new Uri(configuredPath, UriKind.Absolute);
        }

        return fallbackUri;
    }

    private sealed class LoopingTrack
    {
        private MediaPlayer? _player;
        private DispatcherTimer? _restartTimer;
        private Uri? _source;
        private double _volume;
        private int _loopIntervalSec;

        public void Start(Uri? sourceUri, double volume, int loopIntervalSec)
        {
            if (sourceUri is null)
            {
                return;
            }

            Stop();

            _source = sourceUri;
            _volume = Math.Clamp(volume / 100d, 0d, 1d);
            _loopIntervalSec = Math.Max(0, loopIntervalSec);
            _player = new MediaPlayer();
            _player.MediaEnded += HandleMediaEnded;
            _player.MediaFailed += HandleMediaFailed;
            _player.Open(_source);
            _player.Volume = _volume;
            _player.Play();
        }

        public void Stop()
        {
            if (_restartTimer is not null)
            {
                _restartTimer.Stop();
                _restartTimer.Tick -= HandleRestartTick;
                _restartTimer = null;
            }

            if (_player is not null)
            {
                _player.MediaEnded -= HandleMediaEnded;
                _player.MediaFailed -= HandleMediaFailed;
                _player.Stop();
                _player.Close();
                _player = null;
            }
        }

        private void HandleMediaEnded(object? sender, EventArgs e)
        {
            if (_player is null)
            {
                return;
            }

            if (_loopIntervalSec == 0)
            {
                RestartPlayback();
                return;
            }

            _restartTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_loopIntervalSec)
            };
            _restartTimer.Tick += HandleRestartTick;
            _restartTimer.Start();
        }

        private void HandleRestartTick(object? sender, EventArgs e)
        {
            if (_restartTimer is not null)
            {
                _restartTimer.Stop();
                _restartTimer.Tick -= HandleRestartTick;
                _restartTimer = null;
            }

            RestartPlayback();
        }

        private void RestartPlayback()
        {
            if (_player is null)
            {
                return;
            }

            _player.Position = TimeSpan.Zero;
            _player.Volume = _volume;
            _player.Play();
        }

        private void HandleMediaFailed(object? sender, ExceptionEventArgs e)
        {
            AppLog.Warn($"Audio playback failed: {e.ErrorException.Message}");
            Stop();
        }
    }
}
