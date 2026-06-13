namespace GuitarToolkit.Core.Generation;

public sealed class MelodyModelInput
{
    public string Style { get; init; } = "Metal";

    public string Mode { get; init; } = "NaturalMinor";

    public string Mood { get; init; } = "Dark";

    public string Meter { get; init; } = "4/4";

    public int Bars { get; init; } = 1;

    public string PhraseProfile { get; init; } = "PROFILE_BALANCED";

    public string Density { get; init; } = "DENSITY_MEDIUM";

    public string Contour { get; init; } = "CONTOUR_ARCH";

    public string NoteCount { get; init; } = "NOTE_COUNT_8_12";

    public IReadOnlyList<string> ProgressionRomanNumerals { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PreviousPhraseTokens { get; init; } = Array.Empty<string>();
}

public sealed class MelodyModelOutput
{
    public string ModelName { get; init; } = string.Empty;

    public bool IsAvailable { get; init; }

    public string Status { get; init; } = string.Empty;

    public IReadOnlyList<ModelTokenProbability> NextTokenProbabilities { get; init; } = Array.Empty<ModelTokenProbability>();
}

public interface IMelodyNextTokenModel
{
    MelodyModelOutput PredictNext(MelodyModelInput input);
}

public sealed class MelodyGenerationRequest
{
    public string RootNote { get; init; } = "E";

    public string Mode { get; init; } = "NaturalMinor";

    public string Style { get; init; } = "Metal";

    public string Mood { get; init; } = "Dark";

    public string Meter { get; init; } = "4/4";

    public int Bars { get; init; } = 1;

    public double Temperature { get; init; } = 0.85;

    public int TopK { get; init; } = 8;

    public int? Seed { get; init; }

    public string PhraseProfile { get; init; } = "PROFILE_BALANCED";

    public string Density { get; init; } = "DENSITY_MEDIUM";

    public string Contour { get; init; } = "CONTOUR_ARCH";

    public string NoteCount { get; init; } = "NOTE_COUNT_8_12";

    public IReadOnlyList<string> ProgressionRomanNumerals { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SeedPhraseTokens { get; init; } = Array.Empty<string>();
}

public sealed record GeneratedMelodyEvent(
    string Token,
    string DisplayName,
    string Degree,
    double Beats,
    bool IsRest,
    int OctaveOffset = 0);

public sealed class GeneratedMelodyPhrase
{
    public IReadOnlyList<GeneratedMelodyEvent> Events { get; init; } = Array.Empty<GeneratedMelodyEvent>();

    public IReadOnlyList<string> Tokens { get; init; } = Array.Empty<string>();

    public string ModelStatus { get; init; } = string.Empty;

    public bool UsedPrimaryModel { get; init; }
}
