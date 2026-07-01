using GuitarToolkit.Core.DSP;
using GuitarToolkit.Core.Generation;
using GuitarToolkit.Core.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuitarToolkit.UI;

public partial class InspirationView : UserControl, IThemeAware
{
    private readonly ProgressionInspirationService _service;
    private GeneratedProgression? _currentProgression;
    private IAudioPlayback? _audio;

    private static Color AccentColor => ThemeManager.GetColor("AccentBrush");
    private static Color PanelBorder => ThemeManager.GetColor("PanelBorderBrush");
    private static Color ControlBg => ThemeManager.GetColor("ControlBrush");
    private static Color TextColor => ThemeManager.GetColor("TextBrush");
    private static Color MutedColor => ThemeManager.GetColor("MutedTextBrush");
    private static Color DarkColor => ThemeManager.GetColor("DarkBrush");

    public InspirationView()
    {
        InitializeComponent();
        _service = new ProgressionInspirationService(
            new OnnxProgressionModel(RuntimeModelPaths.ResolveSingle("ProgressionNextTokenModel.onnx")),
            new DemoProgressionNextTokenModel());

        BuildControls();
        RenderEmptyState();
    }

    public void Initialize(IAudioPlayback audio)
    {
        _audio = audio;
        GenerateCurrent();
    }

    public void ApplyTheme()
    {
        RenderProgression();
    }

    private void BuildControls()
    {
        RootBox.Items.Clear();
        foreach (string root in ProgressionBuilder.AllRoots)
        {
            RootBox.Items.Add(root);
        }
        RootBox.SelectedItem = "E";

        ModeBox.Items.Clear();
        ModeBox.Items.Add(new OptionItem("NaturalMinor", "Натуральный минор"));
        ModeBox.Items.Add(new OptionItem("Major", "Мажор"));
        ModeBox.Items.Add(new OptionItem("Dorian", "Дорийский"));
        ModeBox.Items.Add(new OptionItem("Phrygian", "Фригийский"));
        ModeBox.Items.Add(new OptionItem("HarmonicMinor", "Гармонический минор"));
        ModeBox.SelectedIndex = 0;

        StyleBox.Items.Clear();
        StyleBox.Items.Add(new OptionItem("Metal", "Метал"));
        StyleBox.Items.Add(new OptionItem("Rock", "Рок"));
        StyleBox.Items.Add(new OptionItem("Pop", "Поп"));
        StyleBox.Items.Add(new OptionItem("Ambient", "Эмбиент"));
        StyleBox.Items.Add(new OptionItem("Blues", "Блюз"));
        StyleBox.SelectedIndex = 0;

        MoodBox.Items.Clear();
        MoodBox.Items.Add(new OptionItem("Dark", "Темное"));
        MoodBox.Items.Add(new OptionItem("Epic", "Эпичное"));
        MoodBox.Items.Add(new OptionItem("Bright", "Светлое"));
        MoodBox.Items.Add(new OptionItem("Calm", "Спокойное"));
        MoodBox.Items.Add(new OptionItem("Tense", "Напряженное"));
        MoodBox.SelectedIndex = 0;

        BarsBox.Items.Clear();
        foreach (int bars in new[] { 2, 3, 4, 6, 8, 12, 16 })
        {
            BarsBox.Items.Add(bars);
        }
        BarsBox.SelectedItem = 4;

        TopKBox.Items.Clear();
        foreach (int topK in new[] { 2, 3, 5, 8, 12 })
        {
            TopKBox.Items.Add(topK);
        }
        TopKBox.SelectedItem = 5;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        GenerateCurrent();
    }

    private void GenerateCurrent()
    {
        _currentProgression = _service.Generate(BuildRequest());
        RenderProgression();
    }

    private GenerationRequest BuildRequest()
    {
        return new GenerationRequest
        {
            RootNote = RootBox.SelectedItem?.ToString() ?? "E",
            Mode = GetSelectedValue(ModeBox, "NaturalMinor"),
            Style = GetSelectedValue(StyleBox, "Metal"),
            Mood = GetSelectedValue(MoodBox, "Dark"),
            Bars = BarsBox.SelectedItem is int bars ? bars : 4,
            Temperature = TemperatureSlider.Value,
            TopK = TopKBox.SelectedItem is int topK ? topK : 5,
            SeedRomanNumerals = ParseSeedTokens(SeedTokensBox.Text)
        };
    }

    private static string GetSelectedValue(ComboBox box, string fallback)
    {
        return box.SelectedItem is OptionItem item ? item.Value : fallback;
    }

    private static IReadOnlyList<string> ParseSeedTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text.Split(new[] { ',', ';', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void RenderProgression()
    {
        if (_currentProgression == null)
        {
            RenderEmptyState();
            return;
        }

        ProgressionItems.Items.Clear();
        for (int i = 0; i < _currentProgression.Chords.Count; i++)
        {
            ProgressionItems.Items.Add(CreateChordCard(i + 1, _currentProgression.Chords[i]));
        }

        ModelStatusText.Text = _currentProgression.ModelStatus;
        ScaleText.Text = _currentProgression.SuggestedScale;
        ExplanationText.Text = _currentProgression.Explanation;
        GuitarHintText.Text = _currentProgression.GuitarHint;
    }

    private void RenderEmptyState()
    {
        ProgressionItems.Items.Clear();
        ProgressionItems.Items.Add(new TextBlock
        {
            Text = "Здесь появится символическая прогрессия модели.",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(MutedColor)
        });
    }

    private Border CreateChordCard(int index, GeneratedChord chord)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(ControlBg),
            BorderBrush = new SolidColorBrush(PanelBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 88
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = index.ToString(),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(MutedColor),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = chord.RomanNumeral,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(AccentColor),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = chord.DisplayName,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(TextColor),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        border.Child = stack;
        return border;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_audio == null)
            return;

        if (_currentProgression == null || _currentProgression.Chords.Count == 0)
        {
            GenerateCurrent();
        }

        if (_currentProgression == null || _currentProgression.Chords.Count == 0)
            return;

        int sampleRate = _audio.SampleRate;
        double tempo = GetPlaybackTempo();
        double volume = GetPlaybackVolume();
        int repeats = LoopCheck.IsChecked == true ? 4 : 1;
        double beatDuration = 60.0 / tempo;
        int samplesPerBeat = (int)(sampleRate * beatDuration);
        int progressionLength = samplesPerBeat * _currentProgression.Chords.Count;
        int bufferLength = progressionLength * repeats;
        var buffer = new float[bufferLength];

        for (int repeat = 0; repeat < repeats; repeat++)
        {
            int repeatOffset = repeat * progressionLength;
            for (int i = 0; i < _currentProgression.Chords.Count; i++)
            {
                var generatedChord = _currentProgression.Chords[i];
                var chord = ChordLibrary.Get(generatedChord.Root, generatedChord.ChordType);
                if (chord == null)
                    continue;

                float[] chordSamples = ChordPlayer.Synthesize(
                    chord,
                    sampleRate,
                    duration: (float)beatDuration * 0.95f,
                    strumDelay: 0.02f);

                int offset = repeatOffset + i * samplesPerBeat;
                int length = Math.Min(chordSamples.Length, bufferLength - offset);
                for (int j = 0; j < length; j++)
                {
                    buffer[offset + j] += chordSamples[j] * (float)volume;
                }
            }
        }

        _audio.PlaySamples(buffer);
        StopButton.Visibility = Visibility.Visible;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _audio?.StopPlayback();
        StopButton.Visibility = Visibility.Collapsed;
    }

    private void Temperature_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TemperatureLabel != null)
        {
            TemperatureLabel.Text = e.NewValue.ToString("0.00");
        }
    }

    private double GetPlaybackTempo()
    {
        if (TempoSlider == null)
            return 96;

        return Math.Clamp(TempoSlider.Value, 50, 220);
    }

    private double GetPlaybackVolume()
    {
        if (VolumeSlider == null)
            return 0.75;

        return Math.Clamp(VolumeSlider.Value / 100.0, 0.0, 1.0);
    }

    private void Tempo_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TempoLabel != null)
        {
            TempoLabel.Text = $"{Math.Round(e.NewValue):0} BPM";
        }
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeLabel != null)
        {
            VolumeLabel.Text = $"{Math.Round(e.NewValue):0}%";
        }
    }

    private sealed class OptionItem
    {
        public OptionItem(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }
}
