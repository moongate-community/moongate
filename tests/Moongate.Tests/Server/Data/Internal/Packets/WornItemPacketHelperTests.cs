using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Internal.Packets;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Data.Internal.Packets;

public sealed class WornItemPacketHelperTests
{
    [Test]
    public void EnqueueVisibleWornItems_ShouldIncludeVirtualMountLayer_WhenCharacterIsMounted()
    {
        var character = new UOMobileEntity
        {
            Id = (Serial)0x00000044u,
            MountedDisplayItemId = 0x3E9F
        };
        var packets = new List<WornItemPacket>();

        WornItemPacketHelper.EnqueueVisibleWornItems(character, packets.Add);

        Assert.Multiple(
            () =>
            {
                Assert.That(packets.Any(packet => packet.Layer == ItemLayerType.Mount), Is.True);
                Assert.That(
                    packets.Single(packet => packet.Layer == ItemLayerType.Mount).Item.Id.IsItem,
                    Is.True
                );
            }
        );
    }
}
