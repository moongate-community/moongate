using Moongate.Http.Plugin.Interfaces.Registration;

namespace Moongate.Tests.Support;

/// <summary>Records registration throttle keys while allowing a test to decide each attempt.</summary>
public sealed class RecordingRegistrationRateLimiter : IRegistrationRateLimiter
{
    private readonly Func<string, int, bool> _decision;
    private readonly Dictionary<string, int> _attempts = new();
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys => _keys;

    public RecordingRegistrationRateLimiter(Func<string, int, bool> decision)
    {
        _decision = decision;
    }

    public bool TryAcquire(string clientKey)
    {
        var attempt = _attempts.GetValueOrDefault(clientKey) + 1;
        _attempts[clientKey] = attempt;
        _keys.Add(clientKey);

        return _decision(clientKey, attempt);
    }
}
