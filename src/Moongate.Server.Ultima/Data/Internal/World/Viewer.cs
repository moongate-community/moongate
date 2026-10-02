using Moongate.Network.Packets.Data.Clients;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Internal.World;

/// <summary>
///     A player's session as the world view knows it: where to send, which packet formats its client reads and which
///     hidden items its account sees; a null version means the newest formats.
/// </summary>
public sealed record Viewer(long SessionId, ClientVersion? Version, AccountType Account = AccountType.Regular);
