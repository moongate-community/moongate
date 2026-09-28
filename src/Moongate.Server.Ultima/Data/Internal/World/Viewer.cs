using Moongate.Network.Packets.Data.Clients;

namespace Moongate.Server.Ultima.Data.Internal.World;

/// <summary>
///     A player's session as the world view knows it: where to send and which packet formats its client reads; a null
///     version means the newest formats.
/// </summary>
public sealed record Viewer(long SessionId, ClientVersion? Version);
