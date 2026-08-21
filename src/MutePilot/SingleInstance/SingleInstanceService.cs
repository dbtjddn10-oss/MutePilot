using System.Diagnostics;
using System.Security.Principal;

namespace MutePilot.SingleInstance;

internal sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex = new(false, CreateMutexName());
    private bool _ownsMutex;
    private bool _disposed;

    public bool TryAcquire(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_ownsMutex)
        {
            return true;
        }

        try
        {
            _ownsMutex = _mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        return _ownsMutex;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
    }

    private static string CreateMutexName()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User?.Value ?? identity.Name;
        var sessionId = Process.GetCurrentProcess().SessionId;
        return $"Local\\MutePilot.SingleInstance.{userSid}.{sessionId}";
    }
}
