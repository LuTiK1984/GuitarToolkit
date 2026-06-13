using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GuitarToolkit.Core.Generation;

public sealed class OnnxMelodyPhraseModel : IMelodyNextTokenModel, IDisposable
{
    private const int MaxContextLength = 16;
    private const int MaxSequenceLength = 32;

    private readonly MelodyVocabulary _vocabulary;
    private InferenceSession? _session;
    private DateTime _loadedWriteTimeUtc;
    private string? _loadError;
    private int _contextLength = MaxContextLength;

    public OnnxMelodyPhraseModel(string modelPath, string vocabularyPath)
    {
        ModelPath = modelPath;
        _vocabulary = MelodyVocabulary.LoadOrDefault(vocabularyPath);
    }

    public string ModelPath { get; }

    public MelodyModelOutput PredictNext(MelodyModelInput input)
    {
        if (string.IsNullOrWhiteSpace(ModelPath) || !File.Exists(ModelPath))
        {
            return new MelodyModelOutput
            {
                ModelName = "MelodyPhraseTransformer.onnx",
                IsAvailable = false,
                Status = "ONNX-модель мелодий не найдена. Используется встроенный генератор."
            };
        }

        if (!EnsureSession())
        {
            return new MelodyModelOutput
            {
                ModelName = Path.GetFileName(ModelPath),
                IsAvailable = false,
                Status = $"ONNX-модель мелодий не загрузилась: {_loadError}"
            };
        }

        try
        {
            long[] contextIds = BuildContextIds(input);
            long[] previousIds = BuildPreviousIds(input.PreviousPhraseTokens);

            using var results = _session!.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("context_tokens", new DenseTensor<long>(contextIds, new[] { 1, contextIds.Length })),
                NamedOnnxValue.CreateFromTensor("previous_tokens", new DenseTensor<long>(previousIds, new[] { 1, previousIds.Length }))
            });

            var logits = results.First(item => item.Name == "next_token_logits").AsTensor<float>();

            return new MelodyModelOutput
            {
                ModelName = Path.GetFileName(ModelPath),
                IsAvailable = true,
                Status = $"ONNX-модель мелодий загружена: {Path.GetFileName(ModelPath)}",
                NextTokenProbabilities = BuildProbabilities(logits)
            };
        }
        catch (Exception ex) when (ex is OnnxRuntimeException or InvalidOperationException or ArgumentException)
        {
            return new MelodyModelOutput
            {
                ModelName = Path.GetFileName(ModelPath),
                IsAvailable = false,
                Status = $"ONNX-inference мелодии не сработал: {ex.Message}"
            };
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }

    private bool EnsureSession()
    {
        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(ModelPath);
        if (_session != null && writeTimeUtc == _loadedWriteTimeUtc)
            return true;

        try
        {
            _session?.Dispose();
            byte[] modelBytes = File.ReadAllBytes(ModelPath);
            _session = new InferenceSession(modelBytes);
            _contextLength = GetContextLength(_session) ?? MaxContextLength;
            _loadedWriteTimeUtc = writeTimeUtc;
            _loadError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OnnxRuntimeException or InvalidOperationException)
        {
            _session?.Dispose();
            _session = null;
            _loadedWriteTimeUtc = default;
            _loadError = ex.Message;
            return false;
        }
    }

    private long[] BuildContextIds(MelodyModelInput input)
    {
        var tokens = new List<string>
        {
            ToStyleToken(input.Style),
            ToModeToken(input.Mode),
            ToMoodToken(input.Mood),
            ToMeterToken(input.Meter),
            $"BARS_{Math.Clamp(input.Bars, 1, 4)}"
        };
        AddIfKnown(tokens, NormalizeContextToken(input.PhraseProfile, "PROFILE_BALANCED"));
        AddIfKnown(tokens, NormalizeContextToken(input.Density, "DENSITY_MEDIUM"));
        AddIfKnown(tokens, NormalizeContextToken(input.Contour, "CONTOUR_ARCH"));
        AddIfKnown(tokens, NormalizeContextToken(input.NoteCount, "NOTE_COUNT_8_12"));
        AddIfKnown(tokens, EstimateSectionToken(input));
        AddIfKnown(tokens, EstimateBeatPositionToken(input));
        tokens.AddRange(input.ProgressionRomanNumerals.Where(token => !string.IsNullOrWhiteSpace(token)));
        string padToken = _vocabulary.ExtraContextTokens.Count > 0
            ? "<PAD>"
            : input.Mode.Contains("major", StringComparison.OrdinalIgnoreCase) ? "I" : "i";
        while (tokens.Count < _contextLength)
        {
            tokens.Add(padToken);
        }

        return tokens
            .Take(_contextLength)
            .Select(token => (long)_vocabulary.GetIdOrUnknown(NormalizeProgressionToken(token.Trim())))
            .ToArray();
    }

    private static int? GetContextLength(InferenceSession session)
    {
        if (!session.InputMetadata.TryGetValue("context_tokens", out var metadata))
            return null;

        var dimensions = metadata.Dimensions;
        if (dimensions.Length < 2 || dimensions[1] <= 0)
            return null;

        return dimensions[1];
    }

    private void AddIfKnown(List<string> tokens, string token)
    {
        if (_vocabulary.TryGetId(token, out _))
            tokens.Add(token);
    }

    private static string NormalizeContextToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string EstimateSectionToken(MelodyModelInput input)
    {
        double targetBeats = GetBeatsPerBar(input.Meter) * Math.Clamp(input.Bars, 1, 4);
        double currentBeats = input.PreviousPhraseTokens.Sum(TokenBeats);
        double ratio = currentBeats / Math.Max(0.25, targetBeats);
        if (ratio < 0.28)
            return "SECTION_BEGIN";
        if (ratio > 0.72)
            return "SECTION_END";
        return "SECTION_MIDDLE";
    }

    private static string EstimateBeatPositionToken(MelodyModelInput input)
    {
        double beatsPerBar = GetBeatsPerBar(input.Meter);
        double currentBeats = input.PreviousPhraseTokens.Sum(TokenBeats);
        double inBar = currentBeats % beatsPerBar;
        if (Math.Abs(inBar) < 0.001)
            return "POS_BAR_START";
        if (beatsPerBar - inBar <= 0.26)
            return "POS_BAR_END";
        if (Math.Abs(inBar - Math.Round(inBar)) < 0.001)
            return "POS_STRONG_BEAT";
        return "POS_WEAK_BEAT";
    }

    private static double TokenBeats(string token)
    {
        string duration = token.Split(':').LastOrDefault() ?? string.Empty;
        return duration switch
        {
            "1/16" => 0.25,
            "1/8" => 0.5,
            "1/4" => 1.0,
            "1/2" => 2.0,
            _ => 0.0
        };
    }

    private static double GetBeatsPerBar(string meter)
    {
        if (meter.Contains("3", StringComparison.OrdinalIgnoreCase))
            return 3.0;
        if (meter.Contains("6", StringComparison.OrdinalIgnoreCase))
            return 3.0;
        return 4.0;
    }

    private long[] BuildPreviousIds(IReadOnlyList<string> previousTokens)
    {
        var phraseTokens = previousTokens
            .Where(token => !string.IsNullOrWhiteSpace(token) && token != "<BOS>" && token != "<EOS>")
            .Select(NormalizePhraseToken)
            .TakeLast(MaxSequenceLength - 1)
            .ToArray();

        if (phraseTokens.Length == 0)
        {
            phraseTokens = new[] { _vocabulary.TryGetId("D:1:4:1/8", out _) ? "D:1:4:1/8" : "D:1:1/8" };
        }

        var tokens = new List<string> { "<BOS>" };
        tokens.AddRange(phraseTokens);
        while (tokens.Count < MaxSequenceLength)
        {
            tokens.Insert(0, "<PAD>");
        }

        return tokens
            .TakeLast(MaxSequenceLength)
            .Select(token => (long)_vocabulary.GetIdOrUnknown(token.Trim()))
            .ToArray();
    }

    private IReadOnlyList<ModelTokenProbability> BuildProbabilities(Tensor<float> logits)
    {
        var tokenLogits = _vocabulary.OutputTokens
            .Select(token => new
            {
                Token = token,
                Id = _vocabulary.GetIdOrUnknown(token)
            })
            .Select(item => new
            {
                item.Token,
                Logit = (double)logits[0, item.Id]
            })
            .ToArray();

        double max = tokenLogits.Max(item => item.Logit);
        var weighted = tokenLogits
            .Select(item => new
            {
                item.Token,
                Weight = Math.Exp(item.Logit - max)
            })
            .ToArray();
        double total = weighted.Sum(item => item.Weight);

        return weighted
            .Select(item => new ModelTokenProbability(item.Token, total <= 0 ? 0 : item.Weight / total))
            .OrderByDescending(item => item.Probability)
            .ToArray();
    }

    private static string ToStyleToken(string style)
    {
        if (style.Contains("rock", StringComparison.OrdinalIgnoreCase))
            return "STYLE_ROCK";
        if (style.Contains("pop", StringComparison.OrdinalIgnoreCase))
            return "STYLE_POP";
        if (style.Contains("ambient", StringComparison.OrdinalIgnoreCase))
            return "STYLE_AMBIENT";
        if (style.Contains("blues", StringComparison.OrdinalIgnoreCase))
            return "STYLE_BLUES";
        return "STYLE_METAL";
    }

    private static string ToModeToken(string mode)
    {
        if (mode.Contains("harmonic", StringComparison.OrdinalIgnoreCase))
            return "MODE_HARMONIC_MINOR";
        if (mode.Contains("dorian", StringComparison.OrdinalIgnoreCase))
            return "MODE_DORIAN";
        if (mode.Contains("phrygian", StringComparison.OrdinalIgnoreCase))
            return "MODE_PHRYGIAN";
        if (mode.Contains("major", StringComparison.OrdinalIgnoreCase))
            return "MODE_MAJOR";
        return "MODE_NATURAL_MINOR";
    }

    private static string ToMoodToken(string mood)
    {
        if (mood.Contains("epic", StringComparison.OrdinalIgnoreCase))
            return "MOOD_EPIC";
        if (mood.Contains("bright", StringComparison.OrdinalIgnoreCase))
            return "MOOD_BRIGHT";
        if (mood.Contains("calm", StringComparison.OrdinalIgnoreCase))
            return "MOOD_CALM";
        if (mood.Contains("tense", StringComparison.OrdinalIgnoreCase))
            return "MOOD_TENSE";
        return "MOOD_DARK";
    }

    private static string ToMeterToken(string meter)
    {
        if (meter.Contains("3", StringComparison.OrdinalIgnoreCase))
            return "METER_3_4";
        if (meter.Contains("6", StringComparison.OrdinalIgnoreCase))
            return "METER_6_8";
        return "METER_4_4";
    }

    private static string NormalizeProgressionToken(string token)
    {
        return token
            .Replace("В°", "_dim", StringComparison.Ordinal)
            .Replace("Вє", "_dim", StringComparison.Ordinal);
    }

    private string NormalizePhraseToken(string token)
    {
        token = token.Trim();
        if (_vocabulary.TryGetId(token, out _))
            return token;

        string[] parts = token.Split(':');
        if (parts.Length == 3 && parts[0] == "D")
        {
            string upgraded = $"D:{parts[1]}:4:{parts[2]}";
            if (_vocabulary.TryGetId(upgraded, out _))
                return upgraded;
        }

        if (parts.Length == 4 && parts[0] == "D")
        {
            string downgraded = $"D:{parts[1]}:{parts[3]}";
            if (_vocabulary.TryGetId(downgraded, out _))
                return downgraded;
        }

        return token;
    }
}

