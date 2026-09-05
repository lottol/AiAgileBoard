using System.Security.Principal;

namespace AiAgileBoard.Desktop;

internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private RegisteredWaitHandle? _registration;
    public bool IsPrimary { get; }

    public SingleInstance()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var name = @"Local\AiAgileBoard-" + identity.User!.Value;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-activate");
        _mutex = new Mutex(false, name);
        try
        {
            IsPrimary = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            IsPrimary = true;
        }
    }

    public void ActivatePrimary() => _activation.Set();

    public void Listen(Action activate) => _registration = ThreadPool.RegisterWaitForSingleObject(
        _activation, (_, _) => activate(), null, Timeout.Infinite, false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
        _activation.Dispose();
    }
}
