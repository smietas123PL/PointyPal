using System;
using System.Threading;

namespace PointyPal.Infrastructure;

public sealed class SingleInstanceService : IDisposable
{
    private readonly string _mutexName;
    private readonly string _signalName;
    private readonly AppLogService? _appLog;
    private readonly CancellationTokenSource _listenerCts = new();
    private Mutex? _mutex;
    private EventWaitHandle? _signalEvent;
    private Thread? _listenerThread;
    private bool _disposed;

    public bool IsPrimaryInstance { get; private set; }
    public int SecondInstanceDetectedCount { get; private set; }

    public SingleInstanceService(AppLogService? appLog = null)
        : this(
            @"Local\PointyPal.SingleInstance",
            @"Local\PointyPal.ActivatePrimary",
            appLog)
    {
    }

    internal SingleInstanceService(string mutexName, string signalName, AppLogService? appLog = null)
    {
        _mutexName = mutexName;
        _signalName = signalName;
        _appLog = appLog;
    }

    public bool TryAcquirePrimary(Action? activationRequested = null)
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, _mutexName, out bool createdNew);
            IsPrimaryInstance = createdNew;

            if (!createdNew)
            {
                _appLog?.Info("SecondInstanceLaunchDetected", $"Mutex={_mutexName}");
                SignalExistingInstance();
                return false;
            }

            _signalEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _signalName);
            StartSignalListener(activationRequested);
            return true;
        }
        catch (Exception ex)
        {
            _appLog?.Warning("SingleInstanceUnavailable", $"Error={ex.Message}");
            IsPrimaryInstance = true;
            return true;
        }
    }

    public bool SignalExistingInstance()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(_signalName);
            signal.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _appLog?.Warning("SingleInstanceSignalFailed", $"Error={ex.Message}");
            return false;
        }
    }

    private void StartSignalListener(Action? activationRequested)
    {
        if (_signalEvent == null || activationRequested == null)
        {
            return;
        }

        _listenerThread = new Thread(() =>
        {
            while (!_listenerCts.IsCancellationRequested)
            {
                try
                {
                    if (_signalEvent.WaitOne(TimeSpan.FromMilliseconds(500)))
                    {
                        SecondInstanceDetectedCount++;
                        _appLog?.Info("SecondInstanceSignalReceived", $"Count={SecondInstanceDetectedCount}");
                        activationRequested();
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _appLog?.Warning("SingleInstanceListenerFailed", $"Error={ex.Message}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "PointyPal single-instance listener"
        };
        _listenerThread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listenerCts.Cancel();
        _signalEvent?.Dispose();
        _listenerCts.Dispose();

        if (IsPrimaryInstance)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // Ignore release failures during process shutdown.
            }
        }

        _mutex?.Dispose();
    }
}
