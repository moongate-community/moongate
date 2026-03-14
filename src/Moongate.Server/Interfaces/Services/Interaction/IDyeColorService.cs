using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.Network.Packets.Incoming.Interaction;

namespace Moongate.Server.Interfaces.Services.Interaction;

/// <summary>
/// Manages the classic UO dye-target and hue picker workflow.
/// </summary>
public interface IDyeColorService : IMoongateService
{
    /// <summary>
    /// Starts a dye target cursor flow for a dye tub and opens the hue picker after target validation.
    /// </summary>
    Task<bool> BeginAsync(long sessionId, Serial dyeTubSerial, Func<UOItemEntity, bool>? targetSelectedCallback = null);

    /// <summary>
    /// Opens the dye window directly for a known target item.
    /// </summary>
    Task<bool> SendDyeableAsync(long sessionId, Serial itemSerial, ushort model = 0x0FAB);

    /// <summary>
    /// Handles the hue selected by the client and applies it to the pending target item.
    /// </summary>
    Task<bool> HandleResponseAsync(GameSession session, DyeWindowPacket packet);
}
