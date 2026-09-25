using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Data.Config;

/// <summary>
///     Configures bounded preparation and admission for a TCP listener.
/// </summary>
public sealed record TcpServerOptions
{
    /// <summary>
    ///     Gets the shared stateless framer, when no per-connection framer is supplied.
    /// </summary>
    public INetFramer? Framer { get; init; }

    /// <summary>
    ///     Gets the factory for fresh per-connection pipeline configuration.
    /// </summary>
    public Func<ConnectionPipeline>? ConnectionPipelineFactory { get; init; }

    /// <summary>
    ///     Gets the receive chunk size in bytes.
    /// </summary>
    public int ReceiveBufferSize { get; init; } = 8192;

    /// <summary>
    ///     Gets the maximum complete transport frame size in bytes.
    /// </summary>
    public int MaxFrameLength { get; init; } = 1024 * 1024;

    /// <summary>
    ///     Gets whether to disable Nagle's algorithm.
    /// </summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>
    ///     Gets the maximum admitted connections, including connections being prepared.
    /// </summary>
    public int MaxConnections { get; init; } = 32;

    /// <summary>
    ///     Gets the maximum streams being prepared concurrently.
    /// </summary>
    public int MaxConcurrentPreparations { get; init; } = 8;

    /// <summary>
    ///     Gets the preparation timeout per accepted connection.
    /// </summary>
    public TimeSpan PreparationTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Gets the clock used for preparation deadlines.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    internal void Validate()
    {
        new TcpClientOptions
        {
            ReceiveBufferSize = ReceiveBufferSize,
            MaxFrameLength = MaxFrameLength,
            PreparationTimeout = PreparationTimeout,
            TimeProvider = TimeProvider
        }.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConnections);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxConcurrentPreparations);
    }
}
