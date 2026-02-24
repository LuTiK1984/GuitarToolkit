using GuitarToolkit.Services;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GuitarToolkit.Pages
{
    public partial class MetronomePage : Page
    {
        private MetronomeService _metronome = new();
        private bool _isRunning = false;
        private List<DateTime> _taps = new();
        private bool _pendulumLeft = false;

        public MetronomePage()
        {
            InitializeComponent();
            _metronome.BeatTick += OnBeat;
            VolumeSlider.ValueChanged += (s, e) => _metronome.Volume = (float)e.NewValue;
        }

        // ── BPM слайдер ───────────────────────────────────────────
        private void BpmSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            int bpm = (int)e.NewValue;
            if (BpmLabel != null) BpmLabel.Text = bpm.ToString();
            _metronome.BPM = bpm;
        }

        private void BpmInput_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ApplyBpmInput();
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        private void BpmInput_LostFocus(object s, RoutedEventArgs e)
        {
            ApplyBpmInput();
        }

        private void ApplyBpmInput()
        {
            if (int.TryParse(BpmLabel.Text, out int bpm))
            {
                bpm = Math.Clamp(bpm, 20, 400);
                BpmSlider.Value = bpm;
                _metronome.BPM = bpm;
            }
            else
            {
                // Если ввели мусор — возвращаем текущее значение
                BpmLabel.Text = ((int)BpmSlider.Value).ToString();
            }
        }

        // ── Доли в такте ──────────────────────────────────────────
        private void IncrBeats_Click(object s, RoutedEventArgs e) => SetBeats(_metronome.BeatsPerMeasure + 1);
        private void DecrBeats_Click(object s, RoutedEventArgs e) => SetBeats(_metronome.BeatsPerMeasure - 1);

        private void SetBeats(int value)
        {
            value = Math.Clamp(value, 2, 8);
            _metronome.BeatsPerMeasure = value;
            BeatsLabel.Text = value.ToString();
        }

        // ── Маятник ───────────────────────────────────────────────
        private void OnBeat(int beatIndex)
        {
            Dispatcher.Invoke(() => SwingPendulum(beatIndex));
        }

        private void SwingPendulum(int beatIndex)
        {
            double bpm = BpmSlider.Value;
            double beatDuration = 60000.0 / bpm; // мс на один удар

            double targetAngle = _pendulumLeft ? -28 : 28;
            _pendulumLeft = !_pendulumLeft;

            var anim = new DoubleAnimation
            {
                To = targetAngle,
                Duration = TimeSpan.FromMilliseconds(beatDuration * 0.92),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            PendulumRotate.BeginAnimation(RotateTransform.AngleProperty, anim);
            BobRotate.BeginAnimation(RotateTransform.AngleProperty, anim);

            // Подсветка груза на акцент
            PendulumBob.Fill = beatIndex == 0
                ? new SolidColorBrush(Color.FromRgb(166, 227, 161))
                : new SolidColorBrush(Color.FromRgb(137, 180, 250));
        }

        // ── Tap Tempo ─────────────────────────────────────────────
        private void TapButton_Click(object s, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            _taps.Add(now);

            if (_taps.Count > 1 && (now - _taps[^2]).TotalSeconds > 3)
                _taps.Clear();

            if (_taps.Count >= 2)
            {
                var intervals = new List<double>();
                for (int i = 1; i < _taps.Count; i++)
                    intervals.Add((_taps[i] - _taps[i - 1]).TotalMilliseconds);

                int bpm = (int)(60000 / Average(intervals));
                bpm = Math.Clamp(bpm, 20, 300);
                BpmSlider.Value = bpm;
            }

            if (_taps.Count > 8) _taps.RemoveAt(0);
        }

        private double Average(List<double> list)
        {
            double sum = 0;
            foreach (var v in list) sum += v;
            return sum / list.Count;
        }

        // ── Старт / Стоп ──────────────────────────────────────────
        private void StartStop_Click(object s, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _metronome.Stop();
                StartStopButton.Content = "▶  СТАРТ";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(166, 227, 161));

                // Возвращаем маятник в центр
                var reset = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                };
                PendulumRotate.BeginAnimation(RotateTransform.AngleProperty, reset);
                BobRotate.BeginAnimation(RotateTransform.AngleProperty, reset);
                PendulumBob.Fill = new SolidColorBrush(Color.FromRgb(137, 180, 250));
            }
            else
            {
                _metronome.BPM = (int)BpmSlider.Value;
                _metronome.Volume = (float)VolumeSlider.Value;
                _metronome.Start();
                StartStopButton.Content = "⏹  СТОП";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(243, 139, 168));
            }
            _isRunning = !_isRunning;
        }
    }
}