using MessagePack;
using MessagePack.Resolvers;
using Moongate.Persistence.Entities;

namespace Moongate.Tests.Persistence.Entities;

public class MobileEntityTests
{
    [Fact]
    public void Deserialize_SaveWrittenBeforeTheNewFieldsExisted_KeepsTheDefaults()
    {
        // A mobile persisted before StatCap/FollowersMax existed carries no key for them. The
        // contractless resolver must leave the constructor defaults alone rather than zeroing them,
        // or every existing character would load with a stat cap of 0 and no follower slots.
        var oldSave = new Dictionary<string, object>
        {
            ["Name"] = "Hero",
            ["Strength"] = 50
        };

        var mobile = RoundTrip<Dictionary<string, object>, MobileEntity>(oldSave);

        Assert.Equal("Hero", mobile.Name);
        Assert.Equal(50, mobile.Strength);
        Assert.Equal(225, mobile.StatCap);
        Assert.Equal(5, mobile.FollowersMax);
        Assert.Equal(0, mobile.Followers);
        Assert.Equal(0, mobile.Kills);
        Assert.False(mobile.Criminal);
        Assert.False(mobile.Warmode);
    }

    [Fact]
    public void RoundTrip_CarriesTheNotorietyAndFollowerState()
    {
        var mobile = new MobileEntity
        {
            Name = "Red",
            StatCap = 250,
            Followers = 3,
            FollowersMax = 6,
            Warmode = true,
            Kills = 7,
            Criminal = true
        };

        var loaded = RoundTrip<MobileEntity, MobileEntity>(mobile);

        Assert.Equal(250, loaded.StatCap);
        Assert.Equal(3, loaded.Followers);
        Assert.Equal(6, loaded.FollowersMax);
        Assert.True(loaded.Warmode);
        Assert.Equal(7, loaded.Kills);
        Assert.True(loaded.Criminal);
    }

    // Mirrors the persistence layer, which registers MessagePack's contractless resolver.
    private static TOut RoundTrip<TIn, TOut>(TIn value)
        => MessagePackSerializer.Deserialize<TOut>(
            MessagePackSerializer.Serialize(value, ContractlessStandardResolver.Options),
            ContractlessStandardResolver.Options
        );
}
