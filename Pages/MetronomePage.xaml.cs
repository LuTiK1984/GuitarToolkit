using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GuitarToolkit.Services;

namespace GuitarToolkit.Pages
{
    public partial class MetronomePage : Page
    {
        private MetronomeService _metronome = new();
        private bool _isRunning = false;
        private List<DateTime> _taps = new();
        private List<Ellipse> _beatDots = new();

        public MetronomePage()
        {
            InitializeComponent();
            _metronome.BeatTick += OnBeat;
            BuildBeatDots(4);
            VolumeSlider.ValueChanged += (s, e) => _metronome.Volume = (float)e.NewValue;
        }

        // ── BPM слайдер ──────────────────────────────────────────
        private void BpmSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            int bpm = (int)e.NewValue;
            if (BpmLabel != null) BpmLabel.Text = $"{bpm} BPM";
            _metronome.BPM = bpm;
        }

        // ── Доли в такте ─────────────────────────────────────────
        private void IncrBeats_Click(object s, RoutedEventArgs e) => SetBeats(_metronome.BeatsPerMeasure + 1);
        private void DecrBeats_Click(object s, RoutedEventArgs e) => SetBeats(_metronome.BeatsPerMeasure - 1);

        private void SetBeats(int value)
        {
            value = Math.Clamp(value, 2, 8);
            _metronome.BeatsPerMeasure = value;
            BeatsLabel.Text = value.ToString();
            BuildBeatDots(value);
        }

        // ── Точки-доли ───────────────────────────────────────────
        private void BuildBeatDots(int count)
        {
            BeatsPanel.Items.Clear();
            _beatDots.Clear();

            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(6),
                    Fill = i == 0
                        ? new SolidColorBrush(Color.FromRgb(166, 227, 161)) // акцент — зелёный
                        : new SolidColorBrush(Color.FromRgb(69, 71, 90))    // обычная — серая
                };
                _beatDots.Add(dot);
                BeatsPanel.Items.Add(dot);
            }
        }

        private void OnBeat(int beatIndex)
        {
            Dispatcher.Invoke(() =>
            {
                // Сбрасываем все точки
                for (int i = 0; i < _beatDots.Count; i++)
                {
                    _beatDots[i].Fill = i == 0
                        ? new SolidColorBrush(Color.FromRgb(166, 227, 161))
                        : new SolidColorBrush(Color.FromRgb(69, 71, 90));
                }
                // Подсвечиваем текущую
                if (beatIndex < _beatDots.Count)
                    _beatDots[beatIndex].Fill = new SolidColorBrush(Color.FromRgb(137, 180, 250));
            });
        }

        // ── Tap Tempo ────────────────────────────────────────────
        private void TapButton_Click(object s, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            _taps.Add(now);

            // Сбрасываем если пауза > 3 сек
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

            // Оставляем только последние 8 тапов
            if (_taps.Count > 8) _taps.RemoveAt(0);
        }

        private double Average(List<double> list)
        {
            double sum = 0;
            foreach (var v in list) sum += v;
            return sum / list.Count;
        }

        // ── Старт / Стоп ─────────────────────────────────────────
        private void StartStop_Click(object s, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _metronome.Stop();
                StartStopButton.Content = "▶  СТАРТ";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(166, 227, 161));
            }
            else
            {
                _metronome.BPM = (int)BpmSlider.Value;
                _metronome.Volume = (float)VolumeSlider.Value; // ← вот эта строка
                _metronome.Start();
                StartStopButton.Content = "⏹  СТОП";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(243, 139, 168));
            }
            _isRunning = !_isRunning;
        }
    }
}