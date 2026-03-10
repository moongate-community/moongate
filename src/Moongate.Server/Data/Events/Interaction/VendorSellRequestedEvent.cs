using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Events.Interaction;

/// <summary>
/// Event emitted when a player requests vendor sell via context menu.
/// </summary>
public readonly record struct VendorSellRequestedEvent(
    GameEventBase BaseEvent,
    long SessionId,
    Serial VendorSerial
) : IGameEvent
{
    /// <summary>
    /// Creates a vendor sell request event with current timestamp.
    /// </summary>
    public VendorSellRequestedEvent(long sessionId, Serial vendorSerial)
        : this(GameEventBase.CreateNow(), sessionId, vendorSerial) { }
}
