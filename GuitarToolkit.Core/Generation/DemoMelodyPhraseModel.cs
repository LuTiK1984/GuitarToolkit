namespace GuitarToolkit.Core.Generation;

public sealed class DemoMelodyPhraseModel : IMelodyNextTokenModel
{
    private static readonly string[] HeavyTokens =
    {
        "D:1:1/8", "D:b3:1/8", "D:4:1/16", "D:b2:1/16", "D:5:1/8", "R:1/16"
    };

    private static readonly string[] BrightTokens =
    {
        "D:1:1/8", "D:2:1/8", "D:3:1/8", "D:5:1/8", "D:6:1/4", "R:1/8"
    };

    public MelodyModelOutput PredictNext(MelodyModelInput input)
    {
        var phrase = input.Mood.Contains("bright", StringComparison.OrdinalIgnoreCase)
            || input.Mode.Contains("major", StringComparison.OrdinalIgnoreCase)
                ? BrightTokens
                : HeavyTokens;
        int index = Math.Max(0, input.PreviousPhraseTokens.Count) % phrase.Length;

        var probabilities = phrase
            .Select((token, i) => new ModelTokenProbability(token, i == index ? 0.58 : 0.07))
            .Append(new ModelTokenProbability("<EOS>", input.PreviousPhraseTokens.Count > 10 ? 0.35 : 0.02))
            .OrderByDescending(item => item.Probability)
            .ToArray();

        return new MelodyModelOutput
        {
            ModelName = "DemoMelodyPhraseModel",
            IsAvailable = true,
            Status = "Локальная ONNX-модель мелодий недоступна. Используется встроенный генератор.",
            NextTokenProbabilities = probabilities
        };
    }
}
