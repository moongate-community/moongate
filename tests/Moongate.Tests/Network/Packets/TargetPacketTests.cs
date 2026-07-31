using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets;

/// <summary>
/// The same opcode in both directions, as two records. Nineteen bytes each way, and the cursor id
/// is the whole correlation: an answer that does not carry it back cannot be matched to anything.
/// </summary>
public class TargetPacketTests
{
    [Fact]
    public void TargetCursor_WritesNineteenBytesCarryingTheCursorId()
    {
        var buffer = new byte[64];
        var writer = new SpanWriter(buffer);

        new TargetCursorPacket(0x1234u, TargetSelectionType.Location, TargetCursorType.Neutral).Write(ref writer);

        Assert.Equal(19, writer.Position);
        Assert.Equal(0x6C, buffer[0]);
        Assert.Equal((byte)TargetSelectionType.Location, buffer[1]);
        Assert.Equal(0x1234u, BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(2)));
        Assert.Equal((byte)TargetCursorType.Neutral, buffer[6]);
    }

    // Withdrawing a cursor is the same packet with the cancel type, not a different one.
    [Fact]
    public void TargetCursor_Cancel_UsesTheCancelCursorType()
    {
        var buffer = new byte[64];
        var writer = new SpanWriter(buffer);

        new TargetCursorPacket(0x1234u, TargetSelectionType.Object, TargetCursorType.Cancel).Write(ref writer);

        Assert.Equal((byte)TargetCursorType.Cancel, buffer[6]);
    }

    [Fact]
    public void TargetCursorResponse_ReadsTheClickedSerialAndLocation()
    {
        var reader = new SpanReader(Response(0x1234u, clicked: 0x4000_0001u, x: 100, y: 200, z: 5, graphic: 0x0EED));

        var packet = TargetCursorResponsePacket.Read(ref reader);

        Assert.Equal(0x1234u, packet.CursorId);
        Assert.Equal((Serial)0x4000_0001u, packet.Clicked);
        Assert.Equal(100, packet.Location.X);
        Assert.Equal(200, packet.Location.Y);
        Assert.Equal(5, packet.Location.Z);
        Assert.Equal(0x0EED, packet.Graphic);
    }

    // What the client sends when the player presses Escape: the cursor id it was given, and
    // nothing picked. Interpreting that is the service's job -- the packet only reports it.
    [Fact]
    public void TargetCursorResponse_WithNothingPicked_IsStillReadable()
    {
        var reader = new SpanReader(Response(0x1234u, clicked: 0u, x: 0xFFFF, y: 0xFFFF, z: 0, graphic: 0));

        var packet = TargetCursorResponsePacket.Read(ref reader);

        Assert.Equal(0x1234u, packet.CursorId);
        Assert.Equal(Serial.Zero, packet.Clicked);
    }

    private static byte[] Response(uint cursorId, uint clicked, ushort x, ushort y, sbyte z, ushort graphic)
    {
        var buffer = new byte[19];
        var writer = new SpanWriter(buffer);

        writer.Write((byte)0x6C);
        writer.Write((byte)TargetSelectionType.Object);
        writer.Write(cursorId);
        writer.Write((byte)TargetCursorType.Neutral);
        writer.Write(clicked);
        writer.Write(x);
        writer.Write(y);
        writer.Write((byte)0);
        writer.Write((byte)z);
        writer.Write(graphic);

        return buffer;
    }
}
