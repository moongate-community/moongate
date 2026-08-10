using MessagePack;
using MessagePack.Resolvers;
using Moongate.Persistence.Entities;

namespace Moongate.Tests.Persistence.Entities;

public class ItemEntityTests
{
    [Fact]
    public void NameCliloc_DefaultsToZero()
        => Assert.Equal(0, new ItemEntity().NameCliloc);

    /// <summary>
    /// Every item in every existing world store was written before this field existed. If the
    /// contractless resolver does not leave it at 0, the failure is not "signs have no tooltip" — it
    /// is every item on a live shard losing the name it had, because a non-zero cliloc would be
    /// preferred over the one its graphic implies.
    /// </summary>
    [Fact]
    public void Deserialize_SaveWrittenBeforeTheFieldExisted_ReadsNameClilocAsZero()
    {
        var oldSave = new Dictionary<string, object>
        {
            ["Name"] = "a dagger",
            ["ItemId"] = 3921
        };

        var item = RoundTrip<Dictionary<string, object>, ItemEntity>(oldSave);

        Assert.Equal("a dagger", item.Name);
        Assert.Equal(3921, item.ItemId);
        Assert.Equal(0, item.NameCliloc);
    }

    [Fact]
    public void Serialize_RoundTripsNameCliloc()
    {
        var item = new ItemEntity { Name = "", ItemId = 3032, NameCliloc = 1016093 };

        Assert.Equal(1016093, RoundTrip<ItemEntity, ItemEntity>(item).NameCliloc);
    }

    // Mirrors the persistence layer, which registers MessagePack's contractless resolver.
    private static TOut RoundTrip<TIn, TOut>(TIn value)
        => MessagePackSerializer.Deserialize<TOut>(
            MessagePackSerializer.Serialize(value, ContractlessStandardResolver.Options),
            ContractlessStandardResolver.Options
        );
}
