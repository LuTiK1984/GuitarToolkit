using System;
using NAudio.Wave;
using NAudio.Dsp;

namespace GuitarToolkit.Services
{
    public class AudioService : IDisposable
    {
        private WaveInEvent _waveIn;
        private readonly int SampleRate = 44100;
        private readonly int FftSize = 4096;      // 2^12
        private readonly int FftOrder = 12;        // log2(4096)

        private float[] _ring;
        private int _ringPos = 0;
        private int _filled = 0;

        public float Volume { get; private set; } = 0f;
        public float MicGain { get; set; } = 20f;

        public event Action<string, float, float> NoteDetected;
        public event Action<float> VolumeChanged;

        private float _smoothedFreq = 0f;
        private string _lastNote = "—";
        private int _stableCount = 0;
        private const int StableThreshold = 3;

        public void Start()
        {
            try
            {
                _ring = new float[FftSize];

                // Выводим все доступные устройства
                int deviceCount = WaveIn.DeviceCount;
                System.Diagnostics.Debug.WriteLine($"Микрофонов найдено: {deviceCount}");
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    System.Diagnostics.Debug.WriteLine($"  [{i}] {caps.ProductName}");
                }

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // явно указываем первое устройство
                    WaveFormat = new WaveFormat(SampleRate, 1),
                    BufferMilliseconds = 25
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Запись остановлена: {e.Exception?.Message ?? "без ошибки"}");
                };
                _waveIn.StartRecording();
                System.Diagnostics.Debug.WriteLine("Запись запущена");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start ERROR: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Stop()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
            _smoothedFreq = 0f;
            _stableCount = 0;
            _lastNote = "—";
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            int count = e.BytesRecorded / 2;
            for (int i = 0; i < count; i++)
            {
                short s16 = BitConverter.ToInt16(e.Buffer, i * 2);
                float s = s16 / 32768f; // без усиления для RMS
                _ring[_ringPos & (FftSize - 1)] = s;
                _ringPos++;
            }
            _filled = Math.Min(_filled + count, FftSize);

            float sum = 0;
            for (int i = 0; i < FftSize; i++) sum += _ring[i] * _ring[i];
            Volume = MathF.Sqrt(sum / FftSize);
            VolumeChanged?.Invoke(Volume);

            if (_filled < FftSize) return;
            if (Volume < 0.0005f) // минимальный порог — только полная тишина
            {
                _stableCount = 0;
                _smoothedFreq = 0f;
                return;
            }

            Analyze();
        }

        private void Analyze()
        {
            try
            {
                // Копируем кольцевой буфер по порядку + окно Хэннинга
                var buf = new Complex[FftSize];
                int start = _ringPos % FftSize;
                for (int i = 0; i < FftSize; i++)
                {
                    int idx = (start + i) % FftSize;
                    double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1)));
                    buf[i].X = (float)(_ring[idx] * MicGain * w); // усиление применяем здесь
                    buf[i].Y = 0f;
                }

                // FFT — передаём порядок (12), не размер
                FastFourierTransform.FFT(true, FftOrder, buf);

                // Амплитудный спектр
                int half = FftSize / 2;
                float[] spec = new float[half];
                for (int i = 0; i < half; i++)
                    spec[i] = MathF.Sqrt(buf[i].X * buf[i].X + buf[i].Y * buf[i].Y);

                // HPS — 4 гармоники
                float[] hps = (float[])spec.Clone();
                for (int h = 2; h <= 4; h++)
                    for (int i = 0; i < half / h; i++)
                        hps[i] *= spec[i * h];

                // Поиск пика в диапазоне гитары 70–1400 Hz
                int minB = (int)(70f * FftSize / SampleRate);
                int maxB = (int)(1400f * FftSize / SampleRate);
                int peak = minB;
                float peakVal = 0f;
                for (int i = minB; i < maxB && i < hps.Length; i++)
                {
                    if (hps[i] > peakVal) { peakVal = hps[i]; peak = i; }
                }

                if (peakVal < 0.000001f) return;

                // Параболическая интерполяция
                float freq;
                if (peak > 0 && peak < spec.Length - 1)
                {
                    float l = spec[peak - 1], c = spec[peak], r = spec[peak + 1];
                    float d = 2 * c - l - r;
                    float delta = Math.Abs(d) > 1e-10f ? 0.5f * (r - l) / d : 0f;
                    freq = (peak + delta) * SampleRate / FftSize;
                }
                else
                {
                    freq = peak * SampleRate / FftSize;
                }

                if (freq < 70 || freq > 1400) return;

                var (note, cents) = FrequencyToNote(freq);

                _smoothedFreq = _smoothedFreq < 1f ? freq : _smoothedFreq * 0.5f + freq * 0.5f;
                var (sNote, sCents) = FrequencyToNote(_smoothedFreq);

                if (sNote == _lastNote)
                    _stableCount++;
                else
                {
                    _stableCount = 0;
                    _lastNote = sNote;
                    _smoothedFreq = freq;
                }

                System.Diagnostics.Debug.WriteLine($"Note: {sNote}, Freq: {_smoothedFreq:F1}, Cents: {sCents:F1}, Stable: {_stableCount}");

                if (_stableCount >= StableThreshold)
                    Console.WriteLine($"Note: {sNote}, Freq: {_smoothedFreq:F1}");
                System.Diagnostics.Debug.WriteLine($"Note: {sNote}, Freq: {_smoothedFreq:F1}");
                NoteDetected?.Invoke(sNote, _smoothedFreq, sCents);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFT ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"FFT ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static (string note, float cents) FrequencyToNote(float freq)
        {
            if (freq <= 0) return ("—", 0);
            string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            double semitones = 12.0 * Math.Log2(freq / 440.0) + 69.0;
            int rounded = (int)Math.Round(semitones);
            int noteIndex = ((rounded % 12) + 12) % 12;
            float cents = (float)((semitones - rounded) * 100.0);
            return (notes[noteIndex], cents);
        }

        public void Dispose() => Stop();
    }
}