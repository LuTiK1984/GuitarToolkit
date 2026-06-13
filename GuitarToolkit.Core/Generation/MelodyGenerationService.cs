namespace GuitarToolkit.Core.Generation;

public sealed class MelodyGenerationService
{
    private readonly IMelodyNextTokenModel _primaryModel;
    private readonly IMelodyNextTokenModel _fallbackModel;
    private readonly TemperatureSampler _sampler;

    public MelodyGenerationService(
        IMelodyNextTokenModel primaryModel,
        IMelodyNextTokenModel? fallbackModel = null,
        TemperatureSampler? sampler = null)
    {
        _primaryModel = primaryModel;
        _fallbackModel = fallbackModel ?? new DemoMelodyPhraseModel();
        _sampler = sampler ?? new TemperatureSampler();
    }

    public GeneratedMelodyPhrase Generate(MelodyGenerationRequest request)
    {
        int bars = Math.Clamp(request.Bars, 1, 4);
        double targetBeats = GetBeatsPerBar(request.Meter) * bars;
        int maxTokens = Math.Clamp((int)Math.Ceiling(targetBeats * 4), 8, 64);
        var random = request.Seed.HasValue ? new Random(request.Seed.Value) : new Random();
        var tokens = request.SeedPhraseTokens
            .Where(token => !string.IsNullOrWhiteSpace(token) && token != "<BOS>")
            .ToList();

        bool usedPrimary = true;
        string status = string.Empty;
        double beats = tokens.Sum(GetTokenBeats);

        while (tokens.Count < maxTokens && beats < targetBeats)
        {
            var input = new MelodyModelInput
            {
                Style = request.Style,
                Mode = request.Mode,
                Mood = request.Mood,
                Meter = request.Meter,
                Bars = bars,
                PhraseProfile = request.PhraseProfile,
                Density = request.Density,
                Contour = request.Contour,
                NoteCount = request.NoteCount,
                ProgressionRomanNumerals = request.ProgressionRomanNumerals,
                PreviousPhraseTokens = tokens
            };

            var output = _primaryModel.PredictNext(input);
            if (!output.IsAvailable || output.NextTokenProbabilities.Count == 0)
            {
                usedPrimary = false;
                status = output.Status;
                output = _fallbackModel.PredictNext(input);
            }

            var guardedProbabilities = ApplyMusicalGuards(output.NextTokenProbabilities, tokens, beats, targetBeats);
            string token = _sampler.Sample(guardedProbabilities, request.Temperature, request.TopK, random);
            if (token == "<EOS>")
                break;

            tokens.Add(token);
            beats += GetTokenBeats(token);
        }

        return new GeneratedMelodyPhrase
        {
            Tokens = tokens,
            Events = tokens.Select(ToEvent).Where(item => item != null).Cast<GeneratedMelodyEvent>().ToArray(),
            UsedPrimaryModel = usedPrimary,
            ModelStatus = usedPrimary ? "Использована основная ONNX-модель мелодий." : status
        };
    }

    public static double GetTokenBeats(string token)
    {
        string duration = token.Split(':').LastOrDefault() ?? string.Empty;
        return duration switch
        {
            "1/16" => 0.25,
            "1/8" => 0.5,
            "1/4" => 1.0,
            "1/2" => 2.0,
            _ => 0.5
        };
    }

    public static int GetDegreeSemitone(string degree)
    {
        return degree switch
        {
            "1" => 0,
            "b2" => 1,
            "2" => 2,
            "b3" => 3,
            "3" => 4,
            "4" => 5,
            "#4" => 6,
            "5" => 7,
            "b6" => 8,
            "6" => 9,
            "b7" => 10,
            "7" => 11,
            "8" => 12,
            _ => 0
        };
    }

    private static IReadOnlyList<ModelTokenProbability> ApplyMusicalGuards(
        IReadOnlyList<ModelTokenProbability> probabilities,
        IReadOnlyList<string> previousTokens,
        double currentBeats,
        double targetBeats)
    {
        if (probabilities.Count == 0)
            return probabilities;

        int? lastMidi = LastNoteMidi(previousTokens);
        int repeatCount = TrailingNoteRepeatCount(previousTokens, lastMidi);
        int restStreak = TrailingRestCount(previousTokens);
        string? lastDuration = LastDuration(previousTokens);
        int durationStreak = TrailingDurationCount(previousTokens, lastDuration);
        double restBeats = previousTokens.Where(token => token.StartsWith("R:", StringComparison.Ordinal)).Sum(GetTokenBeats);
        int eventCount = previousTokens.Count(token => token.StartsWith("D:", StringComparison.Ordinal) || token.StartsWith("R:", StringComparison.Ordinal));
        double densityLimit = Math.Max(4.0, targetBeats * 0.70);
        double remainingBeats = Math.Max(0.0, targetBeats - currentBeats);

        return probabilities
            .Select(item =>
            {
                double weight = Math.Max(item.Probability, 0.000001);
                string token = item.Token;

                if (token == "<EOS>" && currentBeats < targetBeats * 0.75)
                    weight *= 0.05;

                if (token.StartsWith("R:", StringComparison.Ordinal))
                {
                    if (restStreak >= 1)
                        weight *= 0.12;
                    if (restBeats > targetBeats * 0.25)
                        weight *= 0.35;
                }

                int? tokenMidi = TokenMidi(token);
                if (lastMidi.HasValue && tokenMidi.HasValue && tokenMidi.Value == lastMidi.Value)
                {
                    if (repeatCount >= 2)
                        weight *= 0.08;
                    else if (repeatCount == 1)
                        weight *= 0.45;
                }

                string? duration = TokenDuration(token);
                if (!string.IsNullOrWhiteSpace(duration) && duration == lastDuration && durationStreak >= 3)
                    weight *= 0.35;

                if (eventCount >= densityLimit)
                {
                    if (duration is "1/16")
                        weight *= 0.18;
                    else if (duration is "1/8")
                        weight *= 0.45;
                    else if (duration is "1/4")
                        weight *= 1.15;
                    else if (duration is "1/2")
                        weight *= 1.35;
                }

                if (remainingBeats <= 0.25 && token != "<EOS>" && GetTokenBeats(token) > remainingBeats + 0.01)
                    weight *= 0.10;

                return new ModelTokenProbability(token, weight);
            })
            .OrderByDescending(item => item.Probability)
            .ToArray();
    }

    private static GeneratedMelodyEvent? ToEvent(string token)
    {
        if (token.StartsWith("R:", StringComparison.Ordinal))
        {
            double restBeats = GetTokenBeats(token);
            return new GeneratedMelodyEvent(token, $"Пауза {FormatBeats(restBeats)}", "R", restBeats, true);
        }

        if (!token.StartsWith("D:", StringComparison.Ordinal))
            return null;

        string[] parts = token.Split(':');
        if (parts.Length is not 3 and not 4)
            return null;

        double beats = GetTokenBeats(token);
        int octaveOffset = 0;
        if (parts.Length == 4 && int.TryParse(parts[2], out int octave))
            octaveOffset = octave - 4;

        string noteLabel = parts.Length == 4 ? $"{parts[1]}{parts[2]}" : parts[1];
        return new GeneratedMelodyEvent(token, $"{noteLabel} · {FormatBeats(beats)}", parts[1], beats, false, octaveOffset);
    }

    private static int? TokenMidi(string token)
    {
        string[] parts = token.Split(':');
        if (parts.Length == 4 && parts[0] == "D" && int.TryParse(parts[2], out int octave))
            return octave * 12 + GetDegreeSemitone(parts[1]);
        if (parts.Length == 3 && parts[0] == "D")
            return 4 * 12 + GetDegreeSemitone(parts[1]);
        return null;
    }

    private static int? LastNoteMidi(IReadOnlyList<string> tokens)
    {
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            int? midi = TokenMidi(tokens[i]);
            if (midi.HasValue)
                return midi.Value;
        }

        return null;
    }

    private static int TrailingNoteRepeatCount(IReadOnlyList<string> tokens, int? lastMidi)
    {
        if (!lastMidi.HasValue)
            return 0;

        int count = 0;
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].StartsWith("R:", StringComparison.Ordinal))
                break;

            int? midi = TokenMidi(tokens[i]);
            if (!midi.HasValue)
                continue;
            if (midi.Value != lastMidi.Value)
                break;
            count++;
        }

        return count;
    }

    private static int TrailingRestCount(IReadOnlyList<string> tokens)
    {
        int count = 0;
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].StartsWith("R:", StringComparison.Ordinal))
            {
                count++;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tokens[i]) && tokens[i] != "<BOS>")
                break;
        }

        return count;
    }

    private static string? LastDuration(IReadOnlyList<string> tokens)
    {
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            string? duration = TokenDuration(tokens[i]);
            if (!string.IsNullOrWhiteSpace(duration))
                return duration;
        }

        return null;
    }

    private static int TrailingDurationCount(IReadOnlyList<string> tokens, string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        int count = 0;
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            string? current = TokenDuration(tokens[i]);
            if (string.IsNullOrWhiteSpace(current))
                continue;
            if (current != duration)
                break;
            count++;
        }

        return count;
    }

    private static string? TokenDuration(string token)
    {
        string duration = token.Split(':').LastOrDefault() ?? string.Empty;
        return duration is "1/16" or "1/8" or "1/4" or "1/2" ? duration : null;
    }

    private static double GetBeatsPerBar(string meter)
    {
        if (meter.Contains("3", StringComparison.OrdinalIgnoreCase))
            return 3.0;
        if (meter.Contains("6", StringComparison.OrdinalIgnoreCase))
            return 3.0;
        return 4.0;
    }

    private static string FormatBeats(double beats)
    {
        return beats switch
        {
            0.25 => "1/16",
            0.5 => "1/8",
            1.0 => "1/4",
            2.0 => "1/2",
            _ => $"{beats:0.##}"
        };
    }
}

