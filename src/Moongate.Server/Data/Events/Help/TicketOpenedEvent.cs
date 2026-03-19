using Moongate.Server.Data.Events.Base;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Events.Help;

public readonly record struct TicketOpenedEvent(
    GameEventBase BaseEvent,
    Serial TicketId,
    Serial SenderCharacterId,
    Serial SenderAccountId,
    HelpTicketCategory Category,
    string Message,
    int MapId,
    Point3D Location
) : IGameEvent
{
    public TicketOpenedEvent(
        Serial ticketId,
        Serial senderCharacterId,
        Serial senderAccountId,
        HelpTicketCategory category,
        string message,
        int mapId,
        Point3D location
    )
        : this(GameEventBase.CreateNow(), ticketId, senderCharacterId, senderAccountId, category, message, mapId, location) { }
}
