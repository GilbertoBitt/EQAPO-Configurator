using System.IO;
using System.Windows;
using System.Windows.Threading;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;
using Microsoft.Win32;

namespace EQAPO_Configurator.Controls;

public partial class ClipReviewWindow : Wpf.Ui.Controls.FluentWindow
{
    private AudioClip? _clip;
    private System.Windows.Media.MediaPlayer? _player;
    private readonly DispatcherTimer _playbackTimer;
    private bool _isPlaying;
    private int _currentFrameIndex;
    private string? _wavPath;

    public ClipReviewWindow()
    {
        InitializeComponent();
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playbackTimer.Tick += PlaybackTick;
    }

    public void LoadClip(AudioClip clip)
    {
        _clip = clip;
        ClipInfoText.Text = $"{clip.Name}  |  {clip.CapturedAt:yyyy-MM-dd HH:mm:ss}  |  {clip.DurationSeconds:F1}s";
        DurationText.Text = FormatTime(clip.DurationSeconds);

        if (clip.SpectrumFrames.Count > 0)
        {
            TimelineSlider.Maximum = clip.SpectrumFrames.Count - 1;
            ReplaySpectrum.SetEqBands(EqEditor.GetBands());
        }

        _wavPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EQAPO-Configurator", "captures", clip.Id, "audio.wav");
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        if (_player == null)
        {
            _player = new System.Windows.Media.MediaPlayer();

            if (_wavPath != null && File.Exists(_wavPath))
            {
                _player.Open(new Uri(_wavPath));
                _player.Volume = 1.0;
            }
        }

        _player?.Play();
        _isPlaying = true;
        _playbackTimer.Start();

        PlayButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        StopButton.IsEnabled = true;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _player?.Pause();
        _isPlaying = false;
        _playbackTimer.Stop();

        PlayButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = true;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void StopPlayback()
    {
        _player?.Stop();
        _isPlaying = false;
        _playbackTimer.Stop();
        _currentFrameIndex = 0;

        if (_clip?.SpectrumFrames.Count > 0)
            TimelineSlider.Value = 0;

        CurrentTimeText.Text = "0:00";
        PlayButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
    }

    private void PlaybackTick(object? sender, EventArgs e)
    {
        if (_clip == null || _clip.SpectrumFrames.Count == 0) return;

        _currentFrameIndex++;
        if (_currentFrameIndex >= _clip.SpectrumFrames.Count)
        {
            StopPlayback();
            return;
        }

        TimelineSlider.Value = _currentFrameIndex;
        var frame = _clip.SpectrumFrames[_currentFrameIndex];
        ReplaySpectrum.UpdateFrame(frame);

        double timePos = (double)_currentFrameIndex / _clip.SpectrumFrames.Count * _clip.DurationSeconds;
        CurrentTimeText.Text = FormatTime(timePos);
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_clip == null || _isPlaying) return;

        int idx = (int)e.NewValue;
        if (idx >= 0 && idx < _clip.SpectrumFrames.Count)
        {
            ReplaySpectrum.UpdateFrame(_clip.SpectrumFrames[idx]);
            double timePos = (double)idx / _clip.SpectrumFrames.Count * _clip.DurationSeconds;
            CurrentTimeText.Text = FormatTime(timePos);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clip == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "WAV Audio|*.wav|JSON Spectrum|*.json|All Files|*.*",
            FileName = $"{_clip.Name}.wav"
        };

        if (dialog.ShowDialog() == true)
        {
            if (dialog.FilterIndex == 1 && _wavPath != null && File.Exists(_wavPath))
            {
                File.Copy(_wavPath, dialog.FileName, true);
            }
            else if (dialog.FilterIndex == 2)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_clip.SpectrumFrames,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
            }
        }
    }

    private static string FormatTime(double seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins}:{secs:D2}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _player?.Stop();
        _player?.Close();
        _playbackTimer.Stop();
        base.OnClosed(e);
    }
}
