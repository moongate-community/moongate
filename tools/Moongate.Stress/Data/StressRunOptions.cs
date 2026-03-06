namespace Moongate.Stress.Data;

public sealed class StressRunOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 2593;

    public Uri HttpBaseAddress { get; init; } = new("http://localhost:8088", UriKind.Absolute);

    public int Clients { get; init; } = 100;

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(5);

    public int RampUpPerSecond { get; init; } = 10;

    public string UserPrefix { get; init; } = "stress";

    public string UserPassword { get; init; } = "StressPwd#123";

    public string UserRole { get; init; } = "Regular";

    public string? AdminUsername { get; init; }

    public string? AdminPassword { get; init; }

    public int MoveIntervalMs { get; init; } = 300;

    public bool Verbose { get; init; }
}
