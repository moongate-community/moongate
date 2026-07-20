namespace Moongate.Http.Plugin.Interfaces.Registration;

/// <summary>A fixed-window per-caller limiter guarding the public registration endpoint.</summary>
public interface IRegistrationRateLimiter
{
    /// <summary>Records an attempt for <paramref name="clientKey" />; false once the window's budget is spent.</summary>
    bool TryAcquire(string clientKey);
}
