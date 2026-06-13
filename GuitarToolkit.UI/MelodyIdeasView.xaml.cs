using GuitarToolkit.Core.DSP;
using GuitarToolkit.Core.Generation;
using GuitarToolkit.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GuitarToolkit.UI;

public partial class MelodyIdeasView : UserControl, IThemeAware
{
    private static readonly string[] RomanOptions =
        { "I", "ii", "iii", "IV", "V", "vi", "i", "III", "iv", "v", "VI", "VII", "bII", "bVI", "bVII" };
    private static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly Key[] PianoHotkeys =
        { Key.A, Key.W, Key.S, Key.E, Key.D, Key.F, Key.T, Key.G, Key.Y, Key.H, Key.U, Key.J };

    private readonly MelodyGenerationService _service;
    private readonly List<string> _progressionTokens = new() { "i", "VI" };
    private readonly List<GeneratedMelodyEvent> _editableEvents = new();
    private readonly List<Border> _melodyCards = new();
    private CancellationTokenSource? _highlightCancellation;
    private GeneratedMelodyPhrase? _currentPhrase;
    private IAudioPlayback? _audio;
    private int _selectedEventIndex = -1;

    private static Color AccentColor => ThemeManager.GetColor("AccentBrush");
    private static Color PanelBorder => ThemeManager.GetColor("PanelBorderBrush");
    private static Color ControlBg => ThemeManager.GetColor("ControlBrush");
    private static Color TextColor => ThemeManager.GetColor("TextBrush");
    private static Color MutedColor => ThemeManager.GetColor("MutedTextBrush");
    private static Color StartColor => ThemeManager.GetColor("StartBrush");
    private static Color DarkColor => ThemeManager.GetColor("DarkBrush");

    public MelodyIdeasView()
    {
        InitializeComponent();
        _service = new MelodyGenerationService(
            new OnnxMelodyPhraseModel(DefaultModelPath, DefaultVocabularyPath),
            new DemoMelodyPhraseModel());

        BuildControls();
        RenderProgression();
        RenderPiano(-1);
        RenderEmptyState();
        Loaded += (_, _) => Focus();
    }

    private static string ModelDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GuitarToolkit", "models");

    private static string DefaultModelPath => Path.Combine(ModelDirectory, "MelodyPhraseTransformer.onnx");

    private static string DefaultVocabularyPath => Path.Combine(ModelDirectory, "MelodyPhraseTransformer.vocab.json");

    public void Initialize(IAudioPlayback audio)
    {
        _audio = audio;
        GenerateCurrent();
    }

    public void ApplyTheme()
    {
        RenderProgression();
        RenderPhrase();
        RenderPiano(-1);
    }

    private void BuildControls()
    {
        foreach (string root in ProgressionBuilder.AllRoots)
            RootBox.Items.Add(root);
        RootBox.SelectedItem = "E";

        AddOptions(ModeBox,
            new("NaturalMinor", "Натуральный минор"),
            new("Major", "Мажор"),
            new("Dorian", "Дорийский"),
            new("Phrygian", "Фригийский"),
            new("HarmonicMinor", "Гармонический минор"));
        ModeBox.SelectedIndex = 0;
        ModeBox.SelectionChanged += (_, _) => RenderProgression();

        AddOptions(StyleBox, new("Metal", "Метал"), new("Rock", "Рок"), new("Pop", "Поп"), new("Ambient", "Эмбиент"), new("Blues", "Блюз"));
        StyleBox.SelectedIndex = 0;

        AddOptions(MoodBox, new("Dark", "Темное"), new("Epic", "Эпичное"), new("Bright", "Светлое"), new("Calm", "Спокойное"), new("Tense", "Напряженное"));
        MoodBox.SelectedIndex = 0;

        AddOptions(MeterBox, new("4/4", "4/4"), new("3/4", "3/4"), new("6/8", "6/8"));
        MeterBox.SelectedIndex = 0;

        foreach (int bars in new[] { 1, 2, 4 })
            BarsBox.Items.Add(bars);
        BarsBox.SelectedItem = 1;

        foreach (int topK in new[] { 4, 6, 8, 12, 16 })
            TopKBox.Items.Add(topK);
        TopKBox.SelectedItem = 8;

        foreach (string duration in new[] { "1/16", "1/8", "1/4", "1/2" })
            InputDurationBox.Items.Add(duration);
        InputDurationBox.SelectedItem = "1/8";
    }

    private static void AddOptions(ComboBox box, params OptionItem[] items)
    {
        foreach (var item in items)
            box.Items.Add(item);
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => GenerateCurrent();

    private void GenerateCurrent()
    {
        StopPlayback();
        _currentPhrase = _service.Generate(BuildRequest());
        _editableEvents.Clear();
        _editableEvents.AddRange(_currentPhrase.Events);
        NormalizeSketchToTimeline();
        _selectedEventIndex = -1;
        RenderPhrase();
    }

    private MelodyGenerationRequest BuildRequest()
    {
        return new MelodyGenerationRequest
        {
            RootNote = RootBox.SelectedItem?.ToString() ?? "E",
            Mode = GetSelectedValue(ModeBox, "NaturalMinor"),
            Style = GetSelectedValue(StyleBox, "Metal"),
            Mood = GetSelectedValue(MoodBox, "Dark"),
            Meter = GetSelectedValue(MeterBox, "4/4"),
            Bars = BarsBox.SelectedItem is int bars ? bars : 1,
            Temperature = TemperatureSlider.Value,
            TopK = TopKBox.SelectedItem is int topK ? topK : 8,
            ProgressionRomanNumerals = _progressionTokens
        };
    }

    private static string GetSelectedValue(ComboBox box, string fallback) =>
        box.SelectedItem is OptionItem item ? item.Value : fallback;

    private void RenderProgression()
    {
        ProgressionItems.Children.Clear();
        foreach (string token in _progressionTokens.ToArray())
        {
            var button = CreateProgressionCard(token);
            button.ToolTip = "Убрать ступень";
            button.Click += (_, _) =>
            {
                _progressionTokens.Remove(token);
                if (_progressionTokens.Count == 0)
                    _progressionTokens.Add(IsMinorMode() ? "i" : "I");
                RenderProgression();
            };
            ProgressionItems.Children.Add(button);
        }

        var addButton = CreateProgressionAddButton();
        ProgressionItems.Children.Add(addButton);
    }

    private Button CreateProgressionAddButton()
    {
        var button = new Button
        {
            Content = "+",
            Height = 54,
            MinWidth = 68,
            Margin = new Thickness(0, 0, 8, 8),
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(ControlBg),
            BorderBrush = new SolidColorBrush(PanelBorder),
            Foreground = new SolidColorBrush(TextColor),
            Cursor = Cursors.Hand
        };

        var menu = new ContextMenu
        {
            Background = new SolidColorBrush(DarkColor),
            BorderBrush = new SolidColorBrush(PanelBorder),
            Foreground = new SolidColorBrush(TextColor)
        };

        foreach (string token in RomanOptions)
        {
            var item = new MenuItem
            {
                Header = token,
                Background = new SolidColorBrush(DarkColor),
                Foreground = new SolidColorBrush(TextColor),
                FontWeight = FontWeights.Bold
            };
            item.Click += (_, _) =>
            {
                _progressionTokens.Add(token);
                RenderProgression();
            };
            menu.Items.Add(item);
        }

        button.ContextMenu = menu;
        button.Click += (_, _) =>
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        };
        return button;
    }

    private Button CreateProgressionCard(string roman)
    {
        string root = RootBox.SelectedItem?.ToString() ?? "E";
        var step = ResolveProgressionStep(roman, root, ProgressionBuilder.GetDiatonicChords(root, ResolveModeIndex()));
        return new Button
        {
            Content = $"{roman}\n{step.Root}{NormalizeChordType(step.ChordType)}",
            Height = 54,
            MinWidth = 68,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(ControlBg),
            BorderBrush = new SolidColorBrush(PanelBorder),
            Foreground = new SolidColorBrush(TextColor),
            Cursor = Cursors.Hand
        };
    }

    private void RenderPhrase()
    {
        if (_currentPhrase == null)
        {
            RenderEmptyState();
            return;
        }

        MelodyItems.Items.Clear();
        _melodyCards.Clear();
        for (int i = 0; i < _editableEvents.Count; i++)
        {
            var card = CreateMelodyCard(i, _editableEvents[i]);
            _melodyCards.Add(card);
            MelodyItems.Items.Add(card);
        }

        ModelStatusText.Text = _currentPhrase.ModelStatus;
        TokenText.Text = _editableEvents.Count == 0 ? "Токены пока не сгенерированы." : string.Join(", ", _editableEvents.Select(item => item.Token));
        NoteText.Text = _editableEvents.Count == 0 ? "Ноты пока не сгенерированы." : string.Join("   ", _editableEvents.Select(ToDisplayNote));
        UpdateSelection();
    }

    private void RenderEmptyState()
    {
        MelodyItems.Items.Clear();
        MelodyItems.Items.Add(new TextBlock
        {
            Text = "Здесь появится короткая фраза. Сгенерируй ее, а потом доработай как музыкальный набросок.",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(MutedColor)
        });
    }

    private Border CreateMelodyCard(int index, GeneratedMelodyEvent item)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(ControlBg),
            BorderBrush = new SolidColorBrush(PanelBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 92,
            Cursor = Cursors.Hand,
            Tag = index
        };
        border.MouseLeftButtonDown += (_, _) =>
        {
            _selectedEventIndex = index;
            UpdateSelection();
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = (index + 1).ToString(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MutedColor), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = item.IsRest ? "R" : ToDisplayNote(item), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(item.IsRest ? MutedColor : TextColor), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = item.DisplayName, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(AccentColor), HorizontalAlignment = HorizontalAlignment.Center });
        border.Child = stack;
        return border;
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _melodyCards.Count; i++)
        {
            _melodyCards[i].BorderBrush = new SolidColorBrush(i == _selectedEventIndex ? AccentColor : PanelBorder);
            _melodyCards[i].BorderThickness = new Thickness(i == _selectedEventIndex ? 2 : 1);
        }
    }

    private void TransposeUp_Click(object sender, RoutedEventArgs e) => TransposeSelected(1);

    private void TransposeDown_Click(object sender, RoutedEventArgs e) => TransposeSelected(-1);

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEventIndex < 0 || _selectedEventIndex >= _editableEvents.Count)
            return;

        _editableEvents.RemoveAt(_selectedEventIndex);
        _selectedEventIndex = Math.Min(_selectedEventIndex, _editableEvents.Count - 1);
        RenderPhrase();
    }

    private void TransposeSelected(int semitones)
    {
        if (_selectedEventIndex < 0 || _selectedEventIndex >= _editableEvents.Count)
            return;

        GeneratedMelodyEvent item = _editableEvents[_selectedEventIndex];
        if (item.IsRest)
            return;

        string newDegree = DegreeFromSemitone(MelodyGenerationService.GetDegreeSemitone(item.Degree) + semitones);
        string duration = item.Token.Split(':').LastOrDefault() ?? "1/8";
        string token = $"D:{newDegree}:{duration}";
        _editableEvents[_selectedEventIndex] = new GeneratedMelodyEvent(token, $"{newDegree} · {duration}", newDegree, item.Beats, false, item.OctaveOffset);
        RenderPhrase();
    }

    private void AppendNoteFromPiano(int midi)
    {
        int root = Array.IndexOf(NoteNames, RootBox.SelectedItem?.ToString() ?? "E");
        if (root < 0)
            root = 4;

        int semitone = ((midi % 12) + 12) % 12;
        string degree = DegreeFromSemitone(semitone - root);
        string duration = InputDurationBox.SelectedItem?.ToString() ?? "1/8";
        string token = $"D:{degree}:{duration}";
        int octaveOffset = (midi - (60 + semitone)) / 12;
        var note = new GeneratedMelodyEvent(token, $"{degree} · {duration}", degree, MelodyGenerationService.GetTokenBeats(token), false, octaveOffset);

        if (RecordPianoCheck.IsChecked == true)
        {
            _editableEvents.Add(note);
            _selectedEventIndex = _editableEvents.Count - 1;
            _currentPhrase ??= new GeneratedMelodyPhrase { ModelStatus = "Ручной скетч без генерации." };
            RenderPhrase();
        }

        _ = PreviewSingleNoteAsync(note);
    }

    private async void PlayMelody_Click(object sender, RoutedEventArgs e) => await PlayAsync(PlayMode.Melody);

    private async void PlayProgression_Click(object sender, RoutedEventArgs e) => await PlayAsync(PlayMode.Progression);

    private async void PlayTogether_Click(object sender, RoutedEventArgs e) => await PlayAsync(PlayMode.Together);

    private async Task PlayAsync(PlayMode mode)
    {
        if (_audio == null)
            return;

        if (_editableEvents.Count == 0)
            GenerateCurrent();

        var playbackEvents = _editableEvents.ToArray();

        int sampleRate = _audio.SampleRate;
        string root = RootBox.SelectedItem?.ToString() ?? "E";
        double bpm = GetTempo();
        int timelineSamples = BeatsToSamples(GetTimelineBeats(), bpm, sampleRate);
        float[] melody = RenderMelodyAudio(playbackEvents, sampleRate, root, bpm, GetMelodyVolume(), timelineSamples);
        float[] progression = RenderProgressionAudio(sampleRate, root, bpm, timelineSamples, GetProgressionVolume());
        float[] buffer = mode switch
        {
            PlayMode.Progression => progression,
            PlayMode.Together => Mix(melody, progression),
            _ => melody
        };

        if (LoopCheck.IsChecked == true)
            buffer = Repeat(buffer, 4);

        _highlightCancellation?.Cancel();
        _highlightCancellation = new CancellationTokenSource();
        _audio.PlaySamples(buffer);
        StopButton.Visibility = Visibility.Visible;
        if (mode != PlayMode.Progression)
            await HighlightMelodyAsync(playbackEvents, bpm, _highlightCancellation.Token);
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void StopPlayback()
    {
        _highlightCancellation?.Cancel();
        RenderPiano(-1);
        _audio?.StopPlayback();
        StopButton.Visibility = Visibility.Collapsed;
    }

    private async Task HighlightMelodyAsync(IReadOnlyList<GeneratedMelodyEvent> playbackEvents, double bpm, CancellationToken token)
    {
        try
        {
            int repeats = LoopCheck.IsChecked == true ? 4 : 1;
            for (int repeat = 0; repeat < repeats; repeat++)
            {
                for (int index = 0; index < playbackEvents.Count; index++)
                {
                    GeneratedMelodyEvent item = playbackEvents[index];
                    if (token.IsCancellationRequested)
                        return;

                    _selectedEventIndex = Math.Min(index, _editableEvents.Count - 1);
                    UpdateSelection();
                    RenderPiano(item.IsRest ? -1 : ToMidi(item));
                    await Task.Delay(TimeSpan.FromSeconds(item.Beats * 60.0 / bpm), token);
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            RenderPiano(-1);
            StopButton.Visibility = Visibility.Collapsed;
        }
    }

    private float[] RenderMelodyAudio(IReadOnlyList<GeneratedMelodyEvent> events, int sampleRate, string rootNote, double bpm, double volume, int targetSamples)
    {
        double secondsPerBeat = 60.0 / bpm;
        int totalSamples = Math.Max(targetSamples, events.Sum(item => (int)Math.Ceiling(item.Beats * secondsPerBeat * sampleRate)));
        var buffer = new float[totalSamples];
        int offset = 0;

        foreach (GeneratedMelodyEvent item in events)
        {
            int count = Math.Max(1, (int)Math.Round(item.Beats * secondsPerBeat * sampleRate));
            if (!item.IsRest)
            {
                float[] note = NoteSynth.GenerateNote(MidiToFrequency(ToMidi(item)), sampleRate, (float)(item.Beats * secondsPerBeat), (float)volume);
                int length = Math.Min(note.Length, buffer.Length - offset);
                for (int i = 0; i < length; i++)
                    buffer[offset + i] += note[i];
            }

            offset += count;
            if (offset >= buffer.Length)
                break;
        }

        return buffer;
    }

    private float[] RenderProgressionAudio(int sampleRate, string rootNote, double bpm, int targetLength, double volume)
    {
        double secondsPerBeat = 60.0 / bpm;
        double beatsPerChord = GetTimelineBeats() / Math.Max(1, _progressionTokens.Count);
        int samplesPerChord = BeatsToSamples(beatsPerChord, bpm, sampleRate);
        var buffer = new float[targetLength];
        var diatonic = ProgressionBuilder.GetDiatonicChords(rootNote, ResolveModeIndex());

        for (int i = 0; i < _progressionTokens.Count; i++)
        {
            var step = ResolveProgressionStep(_progressionTokens[i], rootNote, diatonic);
            var chord = ChordLibrary.Get(step.Root, step.ChordType);
            if (chord == null)
                continue;

            float[] chordSamples = ChordPlayer.Synthesize(chord, sampleRate, (float)(secondsPerBeat * beatsPerChord * 0.98), 0.018f);
            int offset = i * samplesPerChord;
            int count = Math.Min(chordSamples.Length, buffer.Length - offset);
            for (int j = 0; j < count; j++)
                buffer[offset + j] += chordSamples[j] * (float)volume;
        }

        return buffer;
    }

    private static float[] Mix(float[] left, float[] right)
    {
        int length = Math.Max(left.Length, right.Length);
        var result = new float[length];
        for (int i = 0; i < length; i++)
        {
            float value = 0;
            if (i < left.Length)
                value += left[i] * 0.9f;
            if (i < right.Length)
                value += right[i] * 0.95f;
            result[i] = Math.Clamp(value, -1f, 1f);
        }

        return result;
    }

    private static float[] Repeat(float[] source, int count)
    {
        if (count <= 1 || source.Length == 0)
            return source;

        var result = new float[source.Length * count];
        for (int i = 0; i < count; i++)
            Array.Copy(source, 0, result, i * source.Length, source.Length);
        return result;
    }

    private void RenderPiano(int activeMidi)
    {
        PianoCanvas.Children.Clear();
        const double whiteWidth = 58;
        const double whiteHeight = 88;
        const double blackWidth = 34;
        const double blackHeight = 56;

        for (int octave = 3; octave <= 5; octave++)
            DrawOctave(octave, (octave - 3) * 7 * whiteWidth, activeMidi, whiteWidth, whiteHeight, blackWidth, blackHeight);
    }

    private void DrawOctave(int octave, double leftOffset, int activeMidi, double whiteWidth, double whiteHeight, double blackWidth, double blackHeight)
    {
        int[] whiteSemitones = { 0, 2, 4, 5, 7, 9, 11 };
        var blackSlots = new (int Semitone, double Left)[]
        {
            (1, whiteWidth * 0.72),
            (3, whiteWidth * 1.72),
            (6, whiteWidth * 3.72),
            (8, whiteWidth * 4.72),
            (10, whiteWidth * 5.72)
        };

        for (int slot = 0; slot < whiteSemitones.Length; slot++)
        {
            int midi = (octave + 1) * 12 + whiteSemitones[slot];
            var key = CreatePianoKey(midi, activeMidi, whiteWidth, whiteHeight, false);
            Canvas.SetLeft(key, leftOffset + slot * whiteWidth);
            Canvas.SetTop(key, 0);
            PianoCanvas.Children.Add(key);
        }

        foreach (var (semitone, left) in blackSlots)
        {
            int midi = (octave + 1) * 12 + semitone;
            var key = CreatePianoKey(midi, activeMidi, blackWidth, blackHeight, true);
            Canvas.SetLeft(key, leftOffset + left);
            Canvas.SetTop(key, 0);
            Panel.SetZIndex(key, 2);
            PianoCanvas.Children.Add(key);
        }
    }

    private Border CreatePianoKey(int midi, int activeMidi, double width, double height, bool black)
    {
        int semitone = ((midi % 12) + 12) % 12;
        bool active = midi == activeMidi;
        var key = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(active ? StartColor : black ? DarkColor : Colors.White),
            BorderBrush = new SolidColorBrush(active ? StartColor : black ? PanelBorder : MutedColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0, 0, 4, 4),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = $"{NoteNames[semitone]}{MidiToOctave(midi)}",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(active ? DarkColor : black ? TextColor : DarkColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 7)
            }
        };
        key.MouseLeftButtonDown += (_, _) => AppendNoteFromPiano(midi);
        return key;
    }

    private string ToDisplayNote(GeneratedMelodyEvent item) => item.IsRest ? "R" : $"{NoteNames[ToSemitone(item)]}{MidiToOctave(ToMidi(item))}";

    private int ToSemitone(GeneratedMelodyEvent item)
    {
        int root = Array.IndexOf(NoteNames, RootBox.SelectedItem?.ToString() ?? "E");
        if (root < 0)
            root = 4;
        return (root + MelodyGenerationService.GetDegreeSemitone(item.Degree)) % 12;
    }

    private int ToMidi(GeneratedMelodyEvent item) => 60 + ToSemitone(item) + item.OctaveOffset * 12;

    private static float MidiToFrequency(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);

    private static int MidiToOctave(int midi) => midi / 12 - 1;

    private double GetTempo() => Math.Clamp(TempoSlider.Value, 50, 220);

    private double GetMelodyVolume() => Math.Clamp(MelodyVolumeSlider.Value / 100.0, 0.0, 1.0);

    private double GetProgressionVolume() => Math.Clamp(ProgressionVolumeSlider.Value / 100.0, 0.0, 1.0);

    private double GetTimelineBeats() => GetBeatsPerBar() * (BarsBox.SelectedItem is int bars ? bars : 1);

    private double GetBeatsPerBar()
    {
        string meter = GetSelectedValue(MeterBox, "4/4");
        return meter.Contains("3", StringComparison.OrdinalIgnoreCase) || meter.Contains("6", StringComparison.OrdinalIgnoreCase) ? 3.0 : 4.0;
    }

    private static int BeatsToSamples(double beats, double bpm, int sampleRate) =>
        Math.Max(1, (int)Math.Round(beats * 60.0 / bpm * sampleRate));

    private void NormalizeSketchToTimeline()
    {
        if (_editableEvents.Count == 0)
            return;

        double target = GetTimelineBeats();
        double current = _editableEvents.Sum(item => item.Beats);
        var source = _editableEvents.ToArray();
        int index = 0;
        while (current < target - 0.001 && _editableEvents.Count < 64)
        {
            GeneratedMelodyEvent next = source[index % source.Length];
            if (current + next.Beats > target + 0.001)
                break;

            _editableEvents.Add(next);
            current += next.Beats;
            index++;
        }
    }

    private async Task PreviewSingleNoteAsync(GeneratedMelodyEvent item)
    {
        if (_audio == null || item.IsRest)
            return;

        float[] buffer = RenderMelodyAudio(new[] { item }, _audio.SampleRate, RootBox.SelectedItem?.ToString() ?? "E", GetTempo(), GetMelodyVolume(), 1);
        _audio.PlaySamples(buffer);
        RenderPiano(ToMidi(item));
        await Task.Delay(TimeSpan.FromSeconds(Math.Min(0.35, item.Beats * 60.0 / GetTempo())));
        RenderPiano(-1);
    }

    private ProgressionStep ResolveProgressionStep(string token, string rootNote, IReadOnlyList<ProgressionStep> diatonic)
    {
        string normalized = NormalizeRoman(token);
        return diatonic.FirstOrDefault(item => NormalizeRoman(item.Degree) == normalized)
            ?? BuildBorrowedStep(token, rootNote)
            ?? diatonic[0];
    }

    private static ProgressionStep? BuildBorrowedStep(string token, string rootNote)
    {
        string clean = token.Replace("°", "", StringComparison.Ordinal).Trim();
        int semitone = clean.ToUpperInvariant() switch
        {
            "BII" => 1,
            "II" => 2,
            "BIII" => 3,
            "III" => 4,
            "IV" => 5,
            "V" => 7,
            "BVI" => 8,
            "VI" => 9,
            "BVII" => 10,
            "VII" => 11,
            _ => -1
        };
        if (semitone < 0)
            return null;

        int root = Array.IndexOf(NoteNames, rootNote);
        if (root < 0)
            root = 0;

        string chordRoot = NoteNames[(root + semitone) % 12];
        string type = char.IsLower(clean.FirstOrDefault(char.IsLetter)) ? "m" : "Major";
        return new ProgressionStep(token, chordRoot, type);
    }

    private int ResolveModeIndex()
    {
        string mode = GetSelectedValue(ModeBox, "NaturalMinor");
        if (mode.Contains("harmonic", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (mode.Contains("dorian", StringComparison.OrdinalIgnoreCase))
            return 4;
        if (mode.Contains("phrygian", StringComparison.OrdinalIgnoreCase))
            return 6;
        return IsMinorMode() ? 1 : 0;
    }

    private bool IsMinorMode()
    {
        string mode = GetSelectedValue(ModeBox, "NaturalMinor");
        return mode.Contains("minor", StringComparison.OrdinalIgnoreCase)
            || mode.Contains("dorian", StringComparison.OrdinalIgnoreCase)
            || mode.Contains("phrygian", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoman(string value) =>
        value.Replace("°", "", StringComparison.Ordinal).Replace("b", "", StringComparison.OrdinalIgnoreCase).Trim();

    private static string DegreeFromSemitone(int semitone)
    {
        semitone = (semitone % 12 + 12) % 12;
        return semitone switch
        {
            0 => "1",
            1 => "b2",
            2 => "2",
            3 => "b3",
            4 => "3",
            5 => "4",
            6 => "#4",
            7 => "5",
            8 => "b6",
            9 => "6",
            10 => "b7",
            11 => "7",
            _ => "1"
        };
    }

    private void Temperature_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TemperatureLabel != null)
            TemperatureLabel.Text = e.NewValue.ToString("0.00");
    }

    private void Tempo_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TempoLabel != null)
            TempoLabel.Text = Math.Round(e.NewValue).ToString("0");
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MelodyVolumeLabel != null)
            MelodyVolumeLabel.Text = $"{Math.Round(MelodyVolumeSlider.Value):0}%";
        if (ProgressionVolumeLabel != null)
            ProgressionVolumeLabel.Text = $"{Math.Round(ProgressionVolumeSlider.Value):0}%";
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        for (int i = 0; i < PianoHotkeys.Length; i++)
        {
            if (e.Key == PianoHotkeys[i])
            {
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                AppendNoteFromPiano(60 + i);
                e.Handled = true;
                return;
            }
        }
    }

    private static string NormalizeChordType(string chordType) =>
        chordType.Equals("Major", StringComparison.OrdinalIgnoreCase) ? string.Empty : chordType;

    private enum PlayMode
    {
        Melody,
        Progression,
        Together
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
