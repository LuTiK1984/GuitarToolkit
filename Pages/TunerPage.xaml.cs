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
        }

        // ── Нота получена ─────────────────────────────────────────
        private void OnNoteDetected(string note, float freq, float cents)
        {
            Dispatcher.Invoke(() => UpdateUI(note, freq, cents));
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