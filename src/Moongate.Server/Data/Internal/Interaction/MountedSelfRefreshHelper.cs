using System.Globalization;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Interaction;

/// <summary>
/// Applies rider mount state to the active session and enqueues the minimal self refresh packets.
/// </summary>
public static class MountedSelfRefreshHelper
{
    private const string MountedDisplayItemIdKey = "mounted_display_item_id";

    public static void Refresh(
        GameSession session,
        IOutgoingPacketQueue outgoingPacketQueue,
        UOMobileEntity rider,
        UOMobileEntity? mount,
        bool isMounted
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(outgoingPacketQueue);
        ArgumentNullException.ThrowIfNull(rider);

        rider.MountedMobileId = isMounted && mount is not null ? mount.Id : Serial.Zero;
        rider.MountedDisplayItemId = isMounted && mount is not null ? ResolveMountedDisplayItemId(mount) : 0;
        session.Character = rider;
        session.IsMounted = isMounted;

        if (mount is not null)
        {
            mount.RiderMobileId = isMounted ? rider.Id : Serial.Zero;
        }

        outgoingPacketQueue.Enqueue(session.SessionId, new DrawPlayerPacket(rider));
        outgoingPacketQueue.Enqueue(session.SessionId, new MobileDrawPacket(rider, rider, true, true));
        WornItemPacketHelper.EnqueueVisibleWornItems(
            rider,
            packet => outgoingPacketQueue.Enqueue(session.SessionId, packet)
        );
        outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(rider, 1));

        if (isMounted && mount is not null)
        {
            outgoingPacketQueue.Enqueue(session.SessionId, new DeleteObjectPacket(mount.Id));
        }
    }

    private static int ResolveMountedDisplayItemId(UOMobileEntity mount)
    {
        if (mount.TryGetCustomInteger(MountedDisplayItemIdKey, out var mountedDisplayItemId))
        {
            return (int)mountedDisplayItemId;
        }

        if (mount.TryGetCustomString(MountedDisplayItemIdKey, out var mountedDisplayItemIdRaw) &&
            TryParseDisplayItemId(mountedDisplayItemIdRaw, out var parsedMountedDisplayItemId))
        {
            return parsedMountedDisplayItemId;
        }

        return mount.Body;
    }

    private static bool TryParseDisplayItemId(string? value, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out itemId);
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
    }
}
