using System;
using System.Timers;
using Timer = System.Timers.Timer;
using NAudio.Wave;
namespace GuitarToolkit.Services
{
    public class MetronomeService : IDisposable
    {
        private Timer _timer;
        private int _bpm = 120;
        private int _beatsPerMeasure = 4;
        private int _currentBeat = 0;
        private bool _isRunning = false;

        public event Action<int> BeatTick; // int — номер доли (0 = акцент)

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
            _timer = new Timer();
            _timer.Elapsed += OnTick;
            _timer.AutoReset = true;
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
            PlayClick(_currentBeat == 0);
            BeatTick?.Invoke(_currentBeat);
            _currentBeat = (_currentBeat + 1) % _beatsPerMeasure;
        }

        private double BeatInterval() => 60000.0 / _bpm;

        private void PlayClick(bool isAccent)
        {
            // Генерируем синусоиду — без внешних wav-файлов
            float freq = isAccent ? 1000f : 700f;
            int sampleRate = 44100;
            int durationMs = 30;
            int sampleCount = sampleRate * durationMs / 1000;

            float[] buffer = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount; // затухание
                buffer[i] = MathF.Sin(2 * MathF.PI * freq * t) * envelope * 0.8f;
            }

            var provider = new RawSourceWaveStream(
                FloatToBytes(buffer), 0, buffer.Length * 4,
                WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));

            using var output = new WaveOutEvent();
            output.Init(provider);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                System.Threading.Thread.Sleep(1);
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
        }
    }
}