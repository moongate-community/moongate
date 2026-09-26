namespace Moongate.Network.Packets.Types.Clients;

/// <summary>
///     The family of the UO client, told apart by the major version it reports.
/// </summary>
public enum ClientType : byte
{
    /// <summary>
    ///     The 2D client and its reimplementations, such as ClassicUO.
    /// </summary>
    Classic = 0,

    /// <summary>
    ///     Kingdom Reborn, which reports major version 66.
    /// </summary>
    Kr = 1,

    /// <summary>
    ///     The Enhanced Client, which reports its major version plus 60 (for example 67 for 7).
    /// </summary>
    Enhanced = 2
}
