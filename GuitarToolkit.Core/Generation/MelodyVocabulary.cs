using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuitarToolkit.Core.Generation;

public sealed class MelodyVocabulary
{
    private readonly Dictionary<string, int> _tokenToId;

    public MelodyVocabulary(
        IReadOnlyList<string> specialTokens,
        IReadOnlyList<string> styleTokens,
        IReadOnlyList<string> modeTokens,
        IReadOnlyList<string> moodTokens,
        IReadOnlyList<string> meterTokens,
        IReadOnlyList<string> barTokens,
        IReadOnlyList<string> extraContextTokens,
        IReadOnlyList<string> progressionTokens,
        IReadOnlyList<string> phraseTokens)
    {
        SpecialTokens = specialTokens;
        StyleTokens = styleTokens;
        ModeTokens = modeTokens;
        MoodTokens = moodTokens;
        MeterTokens = meterTokens;
        BarTokens = barTokens;
        ExtraContextTokens = extraContextTokens;
        ProgressionTokens = progressionTokens;
        PhraseTokens = phraseTokens;
        Tokens = specialTokens
            .Concat(styleTokens)
            .Concat(modeTokens)
            .Concat(moodTokens)
            .Concat(meterTokens)
            .Concat(barTokens)
            .Concat(extraContextTokens)
            .Concat(progressionTokens)
            .Concat(phraseTokens)
            .ToArray();

        _tokenToId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Tokens.Count; i++)
        {
            _tokenToId[Tokens[i]] = i;
        }
    }

    public IReadOnlyList<string> SpecialTokens { get; }

    public IReadOnlyList<string> StyleTokens { get; }

    public IReadOnlyList<string> ModeTokens { get; }

    public IReadOnlyList<string> MoodTokens { get; }

    public IReadOnlyList<string> MeterTokens { get; }

    public IReadOnlyList<string> BarTokens { get; }

    public IReadOnlyList<string> ExtraContextTokens { get; }

    public IReadOnlyList<string> ProgressionTokens { get; }

    public IReadOnlyList<string> PhraseTokens { get; }

    public IReadOnlyList<string> Tokens { get; }

    public int Count => Tokens.Count;

    public IReadOnlyList<string> OutputTokens { get; private init; } = Array.Empty<string>();

    public static MelodyVocabulary Default { get; } = CreateDefault();

    public bool TryGetId(string token, out int id) => _tokenToId.TryGetValue(token, out id);

    public int GetIdOrUnknown(string token)
    {
        if (TryGetId(token, out int id))
            return id;

        return _tokenToId["<UNK>"];
    }

    public string GetToken(int id)
    {
        if (id < 0 || id >= Tokens.Count)
            return "<UNK>";

        return Tokens[id];
    }

    public static MelodyVocabulary LoadOrDefault(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Default;

        try
        {
            using var stream = File.OpenRead(path);
            var document = JsonSerializer.Deserialize<VocabularyDocument>(stream);
            if (document == null)
                return Default;

            return FromDocument(document);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return Default;
        }
    }

    private static MelodyVocabulary FromDocument(VocabularyDocument document)
    {
        var vocabulary = new MelodyVocabulary(
            document.SpecialTokens,
            document.StyleTokens,
            document.ModeTokens,
            document.MoodTokens,
            document.MeterTokens,
            document.BarTokens,
            document.ExtraContextTokens,
            document.ProgressionTokens,
            document.PhraseTokens);

        return vocabulary.WithOutput(document.PhraseTokens.Concat(new[] { "<EOS>" }).ToArray());
    }

    private MelodyVocabulary WithOutput(IReadOnlyList<string> outputTokens)
    {
        return new MelodyVocabulary(
            SpecialTokens,
            StyleTokens,
            ModeTokens,
            MoodTokens,
            MeterTokens,
            BarTokens,
            ExtraContextTokens,
            ProgressionTokens,
            PhraseTokens)
        {
            OutputTokens = outputTokens
        };
    }

    private static MelodyVocabulary CreateDefault()
    {
        string[] phraseTokens =
        {
            "D:1:1/16", "D:1:1/8", "D:1:1/4", "D:1:1/2",
            "D:b2:1/16", "D:b2:1/8", "D:b2:1/4", "D:b2:1/2",
            "D:2:1/16", "D:2:1/8", "D:2:1/4", "D:2:1/2",
            "D:b3:1/16", "D:b3:1/8", "D:b3:1/4", "D:b3:1/2",
            "D:3:1/16", "D:3:1/8", "D:3:1/4", "D:3:1/2",
            "D:4:1/16", "D:4:1/8", "D:4:1/4", "D:4:1/2",
            "D:#4:1/16", "D:#4:1/8", "D:#4:1/4", "D:#4:1/2",
            "D:5:1/16", "D:5:1/8", "D:5:1/4", "D:5:1/2",
            "D:b6:1/16", "D:b6:1/8", "D:b6:1/4", "D:b6:1/2",
            "D:6:1/16", "D:6:1/8", "D:6:1/4", "D:6:1/2",
            "D:b7:1/16", "D:b7:1/8", "D:b7:1/4", "D:b7:1/2",
            "D:7:1/16", "D:7:1/8", "D:7:1/4", "D:7:1/2",
            "D:8:1/16", "D:8:1/8", "D:8:1/4", "D:8:1/2",
            "R:1/16", "R:1/8", "R:1/4", "R:1/2"
        };

        var vocabulary = new MelodyVocabulary(
            new[] { "<PAD>", "<UNK>", "<BOS>", "<EOS>" },
            new[] { "STYLE_METAL", "STYLE_ROCK", "STYLE_POP", "STYLE_AMBIENT", "STYLE_BLUES" },
            new[] { "MODE_MAJOR", "MODE_NATURAL_MINOR", "MODE_DORIAN", "MODE_PHRYGIAN", "MODE_HARMONIC_MINOR" },
            new[] { "MOOD_DARK", "MOOD_EPIC", "MOOD_BRIGHT", "MOOD_CALM", "MOOD_TENSE" },
            new[] { "METER_4_4", "METER_3_4", "METER_6_8" },
            new[] { "BARS_1", "BARS_2", "BARS_4" },
            Array.Empty<string>(),
            new[]
            {
                "I", "ii", "iii", "IV", "V", "vi", "vii_dim",
                "i", "ii_dim", "III", "iv", "v", "VI", "VII",
                "bII", "bVI", "bVII"
            },
            phraseTokens);

        return vocabulary.WithOutput(phraseTokens.Concat(new[] { "<EOS>" }).ToArray());
    }

    private sealed class VocabularyDocument
    {
        [JsonPropertyName("special_tokens")]
        public string[] SpecialTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("style_tokens")]
        public string[] StyleTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("mode_tokens")]
        public string[] ModeTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("mood_tokens")]
        public string[] MoodTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("meter_tokens")]
        public string[] MeterTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("bar_tokens")]
        public string[] BarTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("phrase_profile_tokens")]
        public string[] PhraseProfileTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("density_tokens")]
        public string[] DensityTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("contour_tokens")]
        public string[] ContourTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("phrase_section_tokens")]
        public string[] PhraseSectionTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("beat_position_tokens")]
        public string[] BeatPositionTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("note_count_tokens")]
        public string[] NoteCountTokens { get; set; } = Array.Empty<string>();

        [JsonIgnore]
        public IReadOnlyList<string> ExtraContextTokens => PhraseProfileTokens
            .Concat(DensityTokens)
            .Concat(ContourTokens)
            .Concat(PhraseSectionTokens)
            .Concat(BeatPositionTokens)
            .Concat(NoteCountTokens)
            .ToArray();

        [JsonPropertyName("progression_tokens")]
        public string[] ProgressionTokens { get; set; } = Array.Empty<string>();

        [JsonPropertyName("phrase_tokens")]
        public string[] PhraseTokens { get; set; } = Array.Empty<string>();
    }
}
