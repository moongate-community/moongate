namespace Moongate.Network.Data.Config;

/// <summary>Controls connection preparation before outbound application I/O starts.</summary>
public sealed record TcpClientOptions
{
    /// <summary>Gets the per-connection preparation, callbacks and transport pipeline.</summary>
    public ConnectionPipeline Pipeline { get; init; } = new();
    /// <summary>Gets the receive chunk size in bytes.</summary>
    public int ReceiveBufferSize { get; init; } = 8192;
    /// <summary>Gets the maximum complete transport frame size in bytes.</summary>
    public int MaxFrameLength { get; init; } = 1024 * 1024;
    /// <summary>Gets whether to disable Nagle's algorithm.</summary>
    public bool NoDelay { get; init; } = true;
    /// <summary>Gets the timeout for connecting and preparing the transport stream.</summary>
    public TimeSpan PreparationTimeout { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Gets the clock used for preparation deadlines.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(Pipeline);
        ArgumentNullException.ThrowIfNull(TimeProvider);
        if (ReceiveBufferSize is < 1 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize));
        }
        if (MaxFrameLength is < 1 or > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFrameLength));
        }
        if (PreparationTimeout <= TimeSpan.Zero || PreparationTimeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(PreparationTimeout));
        }
    }
}
