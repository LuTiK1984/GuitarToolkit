using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GuitarToolkit.Services;

namespace GuitarToolkit.Pages
{
    public partial class TunerPage : Page
    {
        private AudioService _audio = new();
        private TunerService _tuner = new();

        public TunerPage()
        {
            InitializeComponent();

            // Заполняем строи
            foreach (var t in TunerService.Tunings.Keys)
                TuningBox.Items.Add(t);
            TuningBox.SelectedIndex = 0;

            // Подписка на ноты
            _audio.NoteDetected += OnNoteDetected;
            _audio.Start();
            _audio.VolumeChanged += OnVolumeChanged;
        }
        private void OnVolumeChanged(float volume)
        {
            try
            {
                Dispatcher.Invoke(() => UpdateVolumeBar(volume));
            }
            catch { }
        }

        private void UpdateVolumeBar(float volume)
        {
            // RMS 0..1 → ширина 0..320
            double width = Math.Clamp(volume * 600, 0, 320);
            VolumeBar.Width = width;

            // Цвет по зонам
            if (width < 192)       // зелёная зона — до 60%
                VolumeBar.Fill = new SolidColorBrush(Color.FromRgb(166, 227, 161));
            else if (width < 272)  // жёлтая — 60-85%
                VolumeBar.Fill = new SolidColorBrush(Color.FromRgb(249, 226, 175));
            else                   // красная — выше 85%
                VolumeBar.Fill = new SolidColorBrush(Color.FromRgb(243, 139, 168));
        }

        private void GainSlider_ValueChanged(object s, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audio == null) return;
            // dB → линейный множитель: 0dB=1x, 6dB=2x, 12dB=4x, 18dB=8x, 24dB=16x
            float linear = MathF.Pow(10f, (float)e.NewValue / 20f);
            _audio.MicGain = linear;
            if (GainLabel != null) GainLabel.Text = $"+{e.NewValue:F0} dB";
        }
        // ── Нота получена ─────────────────────────────────────────
        private void OnNoteDetected(string note, float freq, float cents)
        {
            try
            {
                Dispatcher.Invoke(() => UpdateUI(note, freq, cents));
            }
            catch { }
        }

        private void UpdateUI(string note, float freq, float cents)
        {
            NoteLabel.Text = note;
            FreqLabel.Text = $"{freq:F1} Hz";
            CentsLabel.Text = $"{cents:+0.0;-0.0;0} центов";

            // Стрелка — cents от -50 до +50, ширина шкалы 320
            double x = 160 + (cents / 50.0) * 155;
            x = Math.Clamp(x, 5, 315);

            var anim = new DoubleAnimation
            {
                To = x,
                Duration = TimeSpan.FromMilliseconds(80),
            };
            NeedleTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

            // Цвет стрелки и статус
            bool inTune = Math.Abs(cents) < 5;
            bool close = Math.Abs(cents) < 15;

            NeedleArrow.Fill = inTune
                ? new SolidColorBrush(Color.FromRgb(166, 227, 161))
                : close
                    ? new SolidColorBrush(Color.FromRgb(249, 226, 175))
                    : new SolidColorBrush(Color.FromRgb(243, 139, 168));

            if (inTune)
            {
                InTuneLabel.Text = "✓  В СТРОЕ";
                InTuneLabel.Foreground = new SolidColorBrush(Color.FromRgb(166, 227, 161));
                InTuneIndicator.Background = new SolidColorBrush(Color.FromRgb(30, 50, 35));
            }
            else
            {
                InTuneLabel.Text = cents > 0 ? "▼  Понизь" : "▲  Повысь";
                InTuneLabel.Foreground = new SolidColorBrush(Color.FromRgb(137, 180, 250));
                InTuneIndicator.Background = new SolidColorBrush(Color.FromRgb(40, 42, 54));
            }

            // Подсвечиваем ближайшую струну
            HighlightClosestString(note);
        }

        // ── Струны ───────────────────────────────────────────────
        private void TuningBox_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            BuildStrings();
        }

        private void BuildStrings()
        {
            StringsPanel.Items.Clear();
            if (TuningBox.SelectedItem == null) return;

            string key = TuningBox.SelectedItem.ToString();
            var strings = TunerService.Tunings[key];

            for (int i = 0; i < strings.Length; i++)
            {
                int strNum = 6 - i;
                var border = new Border
                {
                    Width = 64,
                    Height = 64,
                    Margin = new Thickness(6),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromRgb(49, 50, 68)),
                    Tag = strings[i]
                };
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                stack.Children.Add(new TextBlock
                {
                    Text = $"{strNum}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(108, 112, 134)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                stack.Children.Add(new TextBlock
                {
                    Text = strings[i],
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(205, 214, 244)),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                border.Child = stack;
                StringsPanel.Items.Add(border);
            }
        }

        private void HighlightClosestString(string detectedNote)
        {
            if (TuningBox.SelectedItem == null) return;
            string key = TuningBox.SelectedItem.ToString();
            var strings = TunerService.Tunings[key];

            foreach (Border b in StringsPanel.Items)
            {
                string strNote = b.Tag?.ToString() ?? "";
                // Убираем октаву для сравнения
                string strNoteName = strNote.Length > 2 ? strNote[..^1] : strNote[..^1];
                bool match = strNoteName == detectedNote;

                b.Background = match
                    ? new SolidColorBrush(Color.FromRgb(30, 60, 80))
                    : new SolidColorBrush(Color.FromRgb(49, 50, 68));
                b.BorderThickness = new Thickness(match ? 2 : 0);
                b.BorderBrush = match
                    ? new SolidColorBrush(Color.FromRgb(137, 180, 250))
                    : Brushes.Transparent;
            }
        }

        // ── Эталон ───────────────────────────────────────────────
        private void RefUp_Click(object s, RoutedEventArgs e) => SetRef(_tuner.ReferenceA + 1);
        private void RefDown_Click(object s, RoutedEventArgs e) => SetRef(_tuner.ReferenceA - 1);

        private void SetRef(float value)
        {
            _tuner.ReferenceA = Math.Clamp(value, 420, 460);
            RefLabel.Text = _tuner.ReferenceA.ToString("F0");
        }
    }
}