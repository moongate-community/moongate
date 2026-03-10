using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a player requests vendor buy via context menu.
/// </summary>
public readonly record struct VendorBuyRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial VendorSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a vendor buy request event with current timestamp.
    /// </summary>
    public VendorBuyRequestedEvent(long sessionId, Serial vendorSerial)
        : this(GameEventBase.CreateNow(), sessionId, vendorSerial) { }
}
