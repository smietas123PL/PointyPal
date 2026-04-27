using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace PointyPal.Voice;

public class AudioPlaybackService : IDisposable
{
    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioFile;
    private readonly object _lock = new();
    
    public bool IsPlaying { get; private set; }

    public async Task PlayAsync(string audioPath, CancellationToken token)
    {
        if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
        {
            return;
        }

        Stop();

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            lock (_lock)
            {
                _waveOut = new WaveOutEvent();
                _audioFile = new AudioFileReader(audioPath);
                _waveOut.Init(_audioFile);
                
                _waveOut.PlaybackStopped += (s, e) =>
                {
                    IsPlaying = false;
                    tcs.TrySetResult(true);
                };

                IsPlaying = true;
                _waveOut.Play();
            }

            // Wait for completion or cancellation
            using (token.Register(() => {
                Stop();
                tcs.TrySetCanceled();
            }))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio playback error: {ex.Message}");
            IsPlaying = false;
            throw;
        }
        finally
        {
            Cleanup();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_waveOut != null)
            {
                _waveOut.Stop();
            }
            IsPlaying = false;
        }
    }

    private void Cleanup()
    {
        lock (_lock)
        {
            if (_waveOut != null)
            {
                _waveOut.Dispose();
                _waveOut = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}
