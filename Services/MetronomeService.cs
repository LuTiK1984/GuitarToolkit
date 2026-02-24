using System;
using System.Timers;
using Timer = System.Timers.Timer;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarToolkit.Services
{
    public class MetronomeService : IDisposable
    {
        private Timer _timer;
        private WaveOutEvent _output;
        private MixingSampleProvider _mixer;

        private float[] _accentBuffer;
        private float[] _normalBuffer;

        private int _bpm = 120;
        private int _beatsPerMeasure = 4;
        private int _currentBeat = 0;
        private bool _isRunning = false;

        public float Volume { get; set; } = 0.8f;
        public event Action<int> BeatTick;

        private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);

        public int BPM
        {
            get => _bpm;
            set
            {
                _bpm = Math.Clamp(value, 20, 300);
                if (_isRunning) Restart();
            }
        }

        public int BeatsPerMeasure
        {
            get => _beatsPerMeasure;
            set { _beatsPerMeasure = value; _currentBeat = 0; }
        }

        public MetronomeService()
        {
            _mixer = new MixingSampleProvider(Format) { ReadFully = true };
            _output = new WaveOutEvent() { DesiredLatency = 100 };
            _output.Init(_mixer);
            _output.Play(); // девайс работает постоянно, просто тишина когда нет звука

            _timer = new Timer();
            _timer.Elapsed += OnTick;
            _timer.AutoReset = true;

            // Заранее готовим оба клика
            _accentBuffer = GenerateClick(1000f, 30);
            _normalBuffer = GenerateClick(700f, 30);
        }

        public void Start()
        {
            _currentBeat = 0;
            _timer.Interval = BeatInterval();
            _timer.Start();
            _isRunning = true;
        }

        public void Stop()
        {
            _timer.Stop();
            _isRunning = false;
            _currentBeat = 0;
        }

        private void Restart()
        {
            _timer.Stop();
            _timer.Interval = BeatInterval();
            _timer.Start();
        }

        private void OnTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            int beat = _currentBeat;
            _currentBeat = (_currentBeat + 1) % _beatsPerMeasure;

            PlayClick(beat == 0);
            BeatTick?.Invoke(beat);
        }

        private double BeatInterval() => 60000.0 / _bpm;

        private void PlayClick(bool isAccent)
        {
            float[] src = isAccent ? _accentBuffer : _normalBuffer;

            // Применяем громкость
            float[] buf = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
                buf[i] = src[i] * Volume;

            // Добавляем в микшер — он воспроизведёт без артефактов
            var provider = new RawSourceWaveStream(
                FloatToBytes(buf), 0, buf.Length * 4, Format)
                .ToSampleProvider();

            _mixer.AddMixerInput(provider);
        }

        private float[] GenerateClick(float freq, int durationMs)
        {
            int sampleRate = 44100;
            int count = sampleRate * durationMs / 1000;
            float[] buffer = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / count;
                buffer[i] = MathF.Sin(2 * MathF.PI * freq * t) * envelope;
            }
            return buffer;
        }

        private byte[] FloatToBytes(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public void Dispose()
        {
            Stop();
            _timer.Dispose();
            _output.Stop();
            _output.Dispose();
        }
    }
}