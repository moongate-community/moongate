using MemoryPack;
using Moongate.Core.Primitives;
using Moongate.Tests.Support.Persistence;

namespace Moongate.Tests.Contract.Persistence;

public sealed class MemoryPackEntityTests
{
    [Fact]
    public void Serial_Serialization_UsesFixedFourByteLittleEndianRepresentation()
    {
        var bytes = MemoryPackSerializer.Serialize(new Serial(0x12345678));

        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, bytes);
        Assert.Equal(new Serial(0x12345678), MemoryPackSerializer.Deserialize<Serial>(bytes));
    }

    [Fact]
    public void VersionTolerantEntity_AddedOrderedMember_PreservesOlderPayload()
    {
        var oldBytes = MemoryPackSerializer.Serialize(new SchemaV1Entity
        {
            Id = new Serial(7),
            Name = "existing"
        });
        var newBytes = MemoryPackSerializer.Serialize(new SchemaV2Entity
        {
            Id = new Serial(8),
            Name = "new",
            Description = "added"
        });

        var evolved = MemoryPackSerializer.Deserialize<SchemaV2Entity>(oldBytes);
        var legacy = MemoryPackSerializer.Deserialize<SchemaV1Entity>(newBytes);

        Assert.NotNull(evolved);
        Assert.Equal(new Serial(7), evolved.Id);
        Assert.Equal("existing", evolved.Name);
        Assert.Null(evolved.Description);
        Assert.NotNull(legacy);
        Assert.Equal(new Serial(8), legacy.Id);
        Assert.Equal("new", legacy.Name);
    }
}
