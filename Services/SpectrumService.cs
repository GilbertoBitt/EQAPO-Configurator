using NAudio.Wave;
using NAudio.Dsp;
using EQAPO_Configurator.Models;
using R3;

namespace EQAPO_Configurator.Services;

public class SpectrumService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private float[] _fftBuffer = [];
    private int _fftWritePos;
    private readonly object _lock = new();
    private readonly SpectrumConfig _config;
    private readonly List<float> _sampleHistory = new();
    private readonly int _maxSampleHistory;
    private volatile bool _isCapturing;
    private readonly double[] _smoothedMagnitudes = [];

    private float[] _lastRmsBuffer = [];
    private readonly object _rmsLock = new();

    private readonly Subject<SpectrumFrame> _frameSubject = new();

    public event Action<SpectrumFrame>? SpectrumUpdated;
    public Observable<SpectrumFrame> Frames => _frameSubject;
    public bool IsCapturing => _isCapturing;

    public SpectrumService(SpectrumConfig? config = null)
    {
        _config = config ?? new SpectrumConfig();
        _fftBuffer = new float[_config.FftSize * 2];
        _maxSampleHistory = 44100 * 10;
        _smoothedMagnitudes = new double[_config.NumBins];
    }

    public bool Start()
    {
        if (_isCapturing) return true;

        try
        {
            _capture = new WasapiLoopbackCapture();

            int channels = _capture.WaveFormat.Channels;
            int rate = _capture.WaveFormat.SampleRate;
            int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;

            _fftBuffer = new float[_config.FftSize * 2];
            _fftWritePos = 0;

            _capture.DataAvailable += OnDataAvailable;

            _capture.StartRecording();
            _isCapturing = true;
            return true;
        }
        catch
        {
            _isCapturing = false;
            return false;
        }
    }

    public void Stop()
    {
        if (!_isCapturing) return;
        _isCapturing = false;

        try
        {
            _capture?.StopRecording();
            _capture?.Dispose();
            _capture = null;
        }
        catch { }

        lock (_lock)
        {
            _sampleHistory.Clear();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isCapturing || e.BytesRecorded == 0) return;

        var format = _capture?.WaveFormat;
        if (format == null) return;

        int channels = format.Channels;
        int bytesPerSample = format.BitsPerSample / 8;
        int bytesPerFrame = channels * bytesPerSample;
        int framesRecorded = e.BytesRecorded / bytesPerFrame;

        float[] monoSamples = new float[framesRecorded];

        for (int i = 0; i < framesRecorded; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = (i * channels + ch) * bytesPerSample;
                if (idx + 4 <= e.BytesRecorded)
                {
                    sum += BitConverter.ToSingle(e.Buffer, idx);
                }
            }
            monoSamples[i] = sum / channels;
        }

        lock (_rmsLock)
        {
            _lastRmsBuffer = monoSamples;
        }

        lock (_lock)
        {
            foreach (float sample in monoSamples)
            {
                _sampleHistory.Add(sample);
                if (_sampleHistory.Count > _maxSampleHistory)
                    _sampleHistory.RemoveAt(0);
            }
        }

        int fftSize = _config.FftSize;
        for (int i = 0; i < monoSamples.Length; i++)
        {
            _fftBuffer[_fftWritePos] = monoSamples[i];
            _fftBuffer[_fftWritePos + fftSize] = 0;
            _fftWritePos++;

            if (_fftWritePos >= fftSize)
            {
                ProcessFft(format.SampleRate, channels);
                _fftWritePos = 0;
            }
        }
    }

    private void ProcessFft(int sampleRate, int channels)
    {
        int fftSize = _config.FftSize;
        var fft = new Complex[fftSize];

        var window = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
            window[i] = (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (fftSize - 1)));

        for (int i = 0; i < fftSize; i++)
        {
            fft[i].X = _fftBuffer[i] * window[i];
            fft[i].Y = 0;
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(fftSize), fft);

        int binCount = _config.NumBins;
        double logMin = Math.Log10(_config.MinFrequency);
        double logMax = Math.Log10(_config.MaxFrequency);
        double nyquist = sampleRate / 2.0;
        double binWidth = nyquist / (fftSize / 2);

        var frequencies = new double[binCount];
        var magnitudes = new double[binCount];

        for (int i = 0; i < binCount; i++)
        {
            double logFreq = logMin + (logMax - logMin) * i / (binCount - 1);
            double targetFreq = Math.Pow(10, logFreq);
            frequencies[i] = targetFreq;

            int binIndex = (int)Math.Round(targetFreq / binWidth);
            binIndex = Math.Clamp(binIndex, 1, fftSize / 2 - 1);

            double real = fft[binIndex].X;
            double imag = fft[binIndex].Y;
            double magnitude = Math.Sqrt(real * real + imag * imag);

            double db = 20 * Math.Log10(Math.Max(magnitude, 1e-10));

            double floorLinear = _config.GainDbFloor;
            db = Math.Clamp(db, floorLinear, _config.GainDbCeiling);

            double smoothing = _config.SmoothingFactor;
            _smoothedMagnitudes[i] = _smoothedMagnitudes[i] * (1 - smoothing) + db * smoothing;
            magnitudes[i] = _smoothedMagnitudes[i];
        }

        float rms = 0;
        lock (_rmsLock)
        {
            if (_lastRmsBuffer.Length > 0)
            {
                double sumSq = 0;
                for (int i = 0; i < _lastRmsBuffer.Length; i++)
                    sumSq += _lastRmsBuffer[i] * _lastRmsBuffer[i];
                rms = (float)Math.Sqrt(sumSq / _lastRmsBuffer.Length);
            }
        }
        double rmsDb = 20 * Math.Log10(Math.Max(rms, 1e-10));

        double peak = 0;
        lock (_rmsLock)
        {
            for (int i = 0; i < _lastRmsBuffer.Length; i++)
            {
                double abs = Math.Abs(_lastRmsBuffer[i]);
                if (abs > peak) peak = abs;
            }
        }
        double peakDb = 20 * Math.Log10(Math.Max(peak, 1e-10));

        var frame = new SpectrumFrame
        {
            Timestamp = DateTime.Now,
            SampleRate = sampleRate,
            FftSize = fftSize,
            Frequencies = frequencies,
            Magnitudes = magnitudes,
            PeakDb = peakDb,
            RmsDb = rmsDb
        };

        SpectrumUpdated?.Invoke(frame);
        _frameSubject.OnNext(frame);
    }

    public List<float> GetCapturedSamples(int maxDurationMs = 30000)
    {
        lock (_lock)
        {
            int maxSamples = maxDurationMs * 44100 / 1000;
            if (_sampleHistory.Count > maxSamples)
                return _sampleHistory.Skip(_sampleHistory.Count - maxSamples).ToList();
            return new List<float>(_sampleHistory);
        }
    }

    public void Dispose()
    {
        Stop();
        _frameSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}
