using System.Collections.Concurrent;
using Moongate.Http.Plugin.Interfaces.Registration;

namespace Moongate.Http.Plugin.Services.Registration;

public sealed class RegistrationRateLimiter : IRegistrationRateLimiter
{
    private readonly TimeProvider _time;
    private readonly int _permitPerWindow;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public RegistrationRateLimiter(TimeProvider time, int permitPerWindow, TimeSpan window)
    {
        _time = time;
        _permitPerWindow = permitPerWindow;
        _window = window;
    }

    public bool TryAcquire(string clientKey)
    {
        var now = _time.GetUtcNow();

        var updated = _windows.AddOrUpdate(
            clientKey,
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= _window
                ? new Window(now, 1)
                : current with { Count = current.Count + 1 }
        );

        return updated.Count <= _permitPerWindow;
    }

    private sealed record Window(DateTimeOffset Start, int Count);
}
