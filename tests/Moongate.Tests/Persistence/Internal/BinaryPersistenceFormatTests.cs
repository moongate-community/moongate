using System.Buffers.Binary;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.Persistence.Internal;

public sealed class BinaryPersistenceFormatTests
{
    [Fact]
    public void WriteHeader_KnownValues_UsesVersionedLittleEndianLayout()
    {
        using var stream = new MemoryStream();
        BinaryPersistenceFormat.WriteHeader(stream, "items", PersistenceFileKind.Journal, 0x0807060504030201, 0);
        var bytes = stream.ToArray();
        Assert.Equal(100, bytes.Length);
        Assert.Equal("MGSTORE1"u8.ToArray(), bytes[..8]);
        Assert.Equal(new byte[] { 1, 0, 2, 0, 5, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, bytes[8..24]);
        Assert.Equal("items"u8.ToArray(), bytes[32..37]);
        Assert.All(bytes[37..96], b => Assert.Equal(0, b));
        Assert.Equal(0xABB65675u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(96)));
        Assert.Equal(0xCBF43926u, BinaryPersistenceFormat.Checksum("123456789"u8));
    }

    [Fact]
    public void WriteRecord_Upsert_EncodesSerialAndBoundedPayload()
    {
        using var stream = new MemoryStream();
        BinaryPersistenceFormat.WriteRecord(stream, PersistenceOperation.Upsert, 1, new(0x12345678), [4, 2]);
        var bytes = stream.ToArray();
        Assert.Equal(34, bytes.Length);
        Assert.Equal("MGR1"u8.ToArray(), bytes[..4]);
        Assert.Equal(1, bytes[4]);
        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12, 2, 0, 0, 0 }, bytes[16..24]);
        Assert.Equal(new byte[] { 4, 2 }, bytes[32..]);
        Assert.Equal(0xCBBBB6D7u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(0x27172C3Eu, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28)));
        Assert.Equal(1UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8)));
    }
}
