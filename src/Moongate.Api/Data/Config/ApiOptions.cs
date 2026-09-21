namespace Moongate.Api.Data.Config;

/// <summary>Finite per-connection and per-endpoint resource limits, copied before use.</summary>
public sealed record ApiOptions
{
    public int MaxFrameLength { get; init; } = 64 * 1024;
    public int MaxPendingCalls { get; init; } = 16;
    public int IncomingQueueCapacity { get; init; } = 16;
    public int OutgoingQueueCapacity { get; init; } = 32;
    public int MaxConnections { get; init; } = 32;
    public int MaxConcurrentHandshakes { get; init; } = 8;
    public int MaxConcurrentHandlers { get; init; } = 8;
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan WriteTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan HandlerTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Rejects unbounded or invalid settings before any socket is opened.</summary>
    public void Validate()
    {
        if (MaxFrameLength is < 1 or > 16 * 1024 * 1024 - 4)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFrameLength));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxPendingCalls);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(IncomingQueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OutgoingQueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentHandshakes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentHandlers);
        ValidateTimeout(HandshakeTimeout);
        ValidateTimeout(CallTimeout);
        ValidateTimeout(WriteTimeout);
        ValidateTimeout(HandlerTimeout);
        ValidateTimeout(ShutdownTimeout);
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "A finite positive timeout is required.");
        }
    }
}
