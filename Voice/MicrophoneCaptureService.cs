using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using NAudio.Wave;

namespace PointyPal.Voice;

public class MicrophoneCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _lastFilePath;
    private bool _isRecording;
    private DateTime _startTime;

    public bool IsRecording => _isRecording;
    public string? LastFilePath => _lastFilePath;
    public double LastDurationMs => _isRecording ? (DateTime.Now - _startTime).TotalMilliseconds : _lastDurationMs;
    private double _lastDurationMs;

    public bool IsMicrophoneAvailable()
    {
        return WaveIn.DeviceCount > 0;
    }

    public void StartRecording(bool saveToDisk = true)
    {
        if (_isRecording) return;
        if (!IsMicrophoneAvailable())
        {
            throw new InvalidOperationException("No microphone detected.");
        }

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string debugDir = Path.Combine(appData, "PointyPal", "debug");
            Directory.CreateDirectory(debugDir);
            
            string fileName = saveToDisk ? "latest-recording.wav" : $"temp-rec-{Guid.NewGuid()}.wav";
            _lastFilePath = Path.Combine(debugDir, fileName);

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1) // 16kHz Mono
            };

            _writer = new WaveFileWriter(_lastFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);
            };

            _waveIn.RecordingStopped += (s, e) =>
            {
                _writer?.Dispose();
                _writer = null;
                _waveIn?.Dispose();
                _waveIn = null;
            };

            _waveIn.StartRecording();
            _isRecording = true;
            _startTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start recording: {ex.Message}");
            _isRecording = false;
            Cleanup();
            throw;
        }
    }

    public void StopRecording()
    {
        if (!_isRecording) return;

        _waveIn?.StopRecording();
        _isRecording = false;
        _lastDurationMs = (DateTime.Now - _startTime).TotalMilliseconds;
    }

    public void CancelRecording()
    {
        if (!_isRecording) return;

        _waveIn?.StopRecording();
        _isRecording = false;
        
        // Wait a bit for the file to be released then delete it
        Task.Delay(100).ContinueWith(_ => {
            try {
                if (_lastFilePath != null && File.Exists(_lastFilePath)) {
                    File.Delete(_lastFilePath);
                }
            } catch { /* ignore */ }
        });
    }

    private void Cleanup()
    {
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        StopRecording();
        Cleanup();
    }
}
