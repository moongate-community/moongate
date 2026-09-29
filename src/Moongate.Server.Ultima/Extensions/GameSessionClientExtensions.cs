using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Which container packet formats the session's client reads. A session whose version is unknown gets the modern
///     formats.
/// </summary>
public static class GameSessionClientExtensions
{
    private static readonly ClientVersion HighSeas = new(7, 0, 9, 0);
    private static readonly ClientVersion ContainerGrid = new(6, 0, 1, 7);

    /// <summary>
    ///     Gets whether the client reads the container type in 0x24 (from 7.0.9.0).
    /// </summary>
    public static bool UsesHighSeasContainers(this GameSession session)
    {
        return session.ClientVersion is not { } version || version.CompareTo(HighSeas) >= 0;
    }

    /// <summary>
    ///     Gets whether the client reads the grid byte in 0x25 and 0x3C (from 6.0.1.7).
    /// </summary>
    public static bool UsesContainerGrid(this GameSession session)
    {
        return session.ClientVersion is not { } version || version.CompareTo(ContainerGrid) >= 0;
    }
}
