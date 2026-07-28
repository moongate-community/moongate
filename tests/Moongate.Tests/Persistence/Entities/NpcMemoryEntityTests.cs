using MessagePack;
using MessagePack.Resolvers;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;

namespace Moongate.Tests.Persistence.Entities;

public class NpcMemoryEntityTests
{
    [Fact]
    public void RoundTrip_CarriesEachScalarTypeAndKey()
    {
        var entity = new NpcMemoryEntity
        {
            Id = new(0x2A),
            Memory =
            {
                ["name"] = NpcMemoryValue.FromString("Bob"),
                ["count"] = NpcMemoryValue.FromNumber(3),
                ["hostile"] = NpcMemoryValue.FromBoolean(true)
            }
        };

        var loaded = RoundTrip(entity);

        Assert.Equal(new(0x2A), loaded.Id);
        Assert.Equal(3, loaded.Memory.Count);

        Assert.Equal(MemoryValueType.String, loaded.Memory["name"].Type);
        Assert.Equal("Bob", loaded.Memory["name"].ToScalar());
        Assert.Equal(3d, loaded.Memory["count"].ToScalar());
        Assert.Equal(true, loaded.Memory["hostile"].ToScalar());
    }

    [Fact]
    public void RoundTrip_EmptyMemory_IsPreserved()
    {
        var loaded = RoundTrip(new() { Id = new(0x1) });

        Assert.Empty(loaded.Memory);
    }

    // Mirrors the persistence layer, which registers MessagePack's contractless resolver.
    private static NpcMemoryEntity RoundTrip(NpcMemoryEntity value)
        => MessagePackSerializer.Deserialize<NpcMemoryEntity>(
            MessagePackSerializer.Serialize(value, ContractlessStandardResolver.Options),
            ContractlessStandardResolver.Options
        );
}
