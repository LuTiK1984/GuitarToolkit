using System;
using NAudio.Wave;
using NAudio.Dsp; // FFT встроен в NAudio

namespace GuitarToolkit.Services
{
    public class AudioService : IDisposable
    {
        private WaveInEvent _waveIn;
        private float[] _sampleBuffer;
        private int _bufferPos = 0;
        private const int SampleRate = 44100;
        private const int FftSize = 4096;
        private float _smoothedFreq = 0f;
        private float _smoothedCents = 0f;
        private string _lastNote = "—";
        private int _stableCount = 0;
        private const int StableThreshold = 3; // сколько похожих измерений подряд нужно
        public float Volume { get; private set; } = 0f; // громкость входящего сигнала
        public event Action<string, float, float> NoteDetected; // нота, частота, центы

        public void Start()
        {
            _sampleBuffer = new float[FftSize];
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, 1),
                BufferMilliseconds = 30
            };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }

        public void Stop()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Конвертим байты в float
            int sampleCount = e.BytesRecorded / 2;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                float normalized = Math.Clamp(sample / 32768f * 4f, -1f, 1f);

                _sampleBuffer[_bufferPos % FftSize] = normalized;
                _bufferPos++;
            }

            // Ждём пока буфер заполнится
            if (_bufferPos < FftSize) return;

            // Считаем громкость входящего сигнала
            float sum = 0;
            for (int i = 0; i < FftSize; i++)
                sum += Math.Abs(_sampleBuffer[i]);
            Volume = sum / FftSize;

            // Слишком тихо — не анализируем
            if (Volume < 0.002f) return;

            Analyze();
        }

        private void Analyze()
        {
            // Копируем буфер и применяем окно Хэннинга
            var fftBuffer = new Complex[FftSize];
            for (int i = 0; i < FftSize; i++)
            {
                float window = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1))));
                fftBuffer[i].X = _sampleBuffer[i] * window; // реальная часть
                fftBuffer[i].Y = 0;                          // мнимая часть
            }

            // FFT
            FastFourierTransform.FFT(true, (int)Math.Log(FftSize, 2), fftBuffer);

            // HPS — Harmonic Product Spectrum (точнее для низких струн)
            float[] spectrum = new float[FftSize / 2];
            for (int i = 0; i < spectrum.Length; i++)
                spectrum[i] = (float)Math.Sqrt(fftBuffer[i].X * fftBuffer[i].X +
                                               fftBuffer[i].Y * fftBuffer[i].Y);

            float[] hps = ApplyHPS(spectrum, 5);

            // Ищем пик — только в диапазоне гитары (70Hz — 1400Hz)
            int minBin = FreqToBin(70);
            int maxBin = FreqToBin(1400);
            int peakBin = minBin;
            float peakVal = 0;
            for (int i = minBin; i < maxBin && i < hps.Length; i++)
            {
                if (hps[i] > peakVal)
                {
                    peakVal = hps[i];
                    peakBin = i;
                }
            }

            // Параболическая интерполяция для точности
            float refinedFreq = RefineFrequency(spectrum, peakBin);

            // Частота → нота + центы
            var (note, cents) = FrequencyToNote(refinedFreq);

            // Сглаживание частоты — берём среднее с предыдущим
            _smoothedFreq = _smoothedFreq == 0
    ? refinedFreq
    : _smoothedFreq * 0.4f + refinedFreq * 0.6f;

            var (smoothNote, smoothCents) = FrequencyToNote(_smoothedFreq);

            // Стабилизация — показываем ноту только если она держится несколько измерений подряд
            if (smoothNote == _lastNote)
            {
                _stableCount++;
            }
            else
            {
                _stableCount = 0;
                _lastNote = smoothNote;
            }

            if (_stableCount >= StableThreshold)
            {
                // Сглаживаем центы отдельно
                _smoothedCents = _smoothedCents * 0.6f + smoothCents * 0.4f;
                NoteDetected?.Invoke(smoothNote, _smoothedFreq, _smoothedCents);
            }
        }

        // HPS — перемножаем спектр с прореженными копиями
        private float[] ApplyHPS(float[] spectrum, int harmonics)
        {
            float[] hps = (float[])spectrum.Clone();
            for (int h = 2; h <= harmonics; h++)
            {
                for (int i = 0; i < hps.Length / h; i++)
                    hps[i] *= spectrum[i * h];
            }
            return hps;
        }

        // Параболическая интерполяция — уточняем частоту между бинами
        private float RefineFrequency(float[] spectrum, int peakBin)
        {
            if (peakBin <= 0 || peakBin >= spectrum.Length - 1)
                return BinToFreq(peakBin);

            float left = spectrum[peakBin - 1];
            float center = spectrum[peakBin];
            float right = spectrum[peakBin + 1];

            float delta = 0.5f * (right - left) / (2 * center - left - right);
            return BinToFreq(peakBin + delta);
        }

        private float BinToFreq(float bin) => bin * SampleRate / FftSize;
        private int FreqToBin(float freq) => (int)(freq * FftSize / SampleRate);

        // Частота → нота + отклонение в центах
        public static (string note, float cents) FrequencyToNote(float freq)
        {
            if (freq <= 0) return ("—", 0);

            string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            // A4 = 440Hz = MIDI 69
            double semitones = 12.0 * Math.Log2(freq / 440.0) + 69.0;

            int rounded = (int)Math.Round(semitones);
            int noteIndex = ((rounded % 12) + 12) % 12;
            float cents = (float)((semitones - rounded) * 100.0);

            return (notes[noteIndex], cents);
        }

        public void Dispose() => Stop();
    }
}