namespace Moongate.Server.Core.Data.Admin;

/// <summary>Absolute administrative session lifetime, independent of listener enablement.</summary>
public sealed class AdminSessionOptions
{
    public TimeSpan Lifetime { get; }

    public AdminSessionOptions(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }
        Lifetime = lifetime;
    }
}
