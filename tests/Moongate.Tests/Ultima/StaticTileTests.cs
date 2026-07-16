using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Moongate.Ultima.Maps;

namespace Moongate.Tests.Ultima;

public class StaticTileTests
{
    [Fact]
    public unsafe void Layout_Is7BytesPacked()
        => Assert.Equal(7, sizeof(StaticTile));

    [Fact]
    public void MemoryMarshalRead_FromRawStaticsRecord_MapsAllMembers()
    {
        // On-disk statics record: ushort id, byte x, byte y, sbyte z, short hue.
        var raw = new byte[7];
        BinaryPrimitives.WriteUInt16LittleEndian(raw, 0x0ECA);
        raw[2] = 3;
        raw[3] = 6;
        raw[4] = unchecked((byte)-5);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(5), 33);

        var tile = MemoryMarshal.Cast<byte, StaticTile>(raw)[0];

        Assert.Equal(0x0ECA, tile.Id);
        Assert.Equal(3, tile.X);
        Assert.Equal(6, tile.Y);
        Assert.Equal(-5, tile.Z);
        Assert.Equal(33, tile.Hue);
    }

    [Fact]
    public void MemoryMarshalWrite_RoundTripsThroughBytes()
    {
        var tile = new StaticTile
        {
            Id = 0x1BC3,
            X = 7,
            Y = 1,
            Z = 42,
            Hue = -2
        };

        var raw = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref tile, 1));
        var roundTripped = MemoryMarshal.Cast<byte, StaticTile>(raw)[0];

        Assert.Equal(0x1BC3, roundTripped.Id);
        Assert.Equal(7, roundTripped.X);
        Assert.Equal(1, roundTripped.Y);
        Assert.Equal(42, roundTripped.Z);
        Assert.Equal(-2, roundTripped.Hue);
    }
}
