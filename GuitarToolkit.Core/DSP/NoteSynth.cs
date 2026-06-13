namespace GuitarToolkit.Core.DSP;

/// <summary>
/// Синтез отдельных нот для тренажёра интервалов.
/// </summary>
public static class NoteSynth
{
    /// <summary>
    /// Синтезирует одну ноту (синусоида + 2-я гармоника + затухание).
    /// </summary>
    public static float[] GenerateNote(float frequency, int sampleRate = 44100,
        float duration = 0.8f, float volume = 0.3f)
    {
        int count = Math.Max(1, (int)(sampleRate * duration));
        float[] buf = new float[count];
        float attackSeconds = Math.Min(0.012f, duration * 0.25f);
        float releaseSeconds = Math.Min(0.08f, duration * 0.4f);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float remaining = duration - t;
            float attack = attackSeconds <= 0f ? 1f : Math.Clamp(t / attackSeconds, 0f, 1f);
            float release = releaseSeconds <= 0f ? 1f : Math.Clamp(remaining / releaseSeconds, 0f, 1f);
            float body = 0.62f + MathF.Exp(-t * 1.6f) * 0.38f;
            float env = attack * release * body;

            float sample = MathF.Sin(2f * MathF.PI * frequency * t) * 0.70f
                         + MathF.Sin(2f * MathF.PI * frequency * 2f * t) * 0.20f
                         + MathF.Sin(2f * MathF.PI * frequency * 3f * t) * 0.08f
                         + MathF.Sin(2f * MathF.PI * frequency * 4f * t) * 0.02f;

            buf[i] = sample * env * volume;
        }

        return buf;
    }

    /// <summary>
    /// Синтезирует два звука последовательно (для интервалов).
    /// </summary>
    public static float[] GenerateInterval(float freq1, float freq2,
        int sampleRate = 44100, float noteDuration = 0.8f, float gap = 0.3f)
    {
        float[] note1 = GenerateNote(freq1, sampleRate, noteDuration);
        float[] note2 = GenerateNote(freq2, sampleRate, noteDuration);
        int gapSamples = (int)(gap * sampleRate);

        float[] result = new float[note1.Length + gapSamples + note2.Length];
        Array.Copy(note1, 0, result, 0, note1.Length);
        Array.Copy(note2, 0, result, note1.Length + gapSamples, note2.Length);

        return result;
    }
}
