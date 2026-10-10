using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Admin.Internal;

/// <summary>
///     The services of the world that the administration of a game host uses: its sessions and characters, the kick, the
///     broadcast, the save and the SQL backup. Login hosts have none.
/// </summary>
internal sealed record AdminWorldServices(
    ISessionService Sessions,
    IMobileService Mobiles,
    IPacketSendService Packets,
    IBroadcastService Broadcast,
    IWorldSaveService Saves,
    ISqlBackupService Backups
);
