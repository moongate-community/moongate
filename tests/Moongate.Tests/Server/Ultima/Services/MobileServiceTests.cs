using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Services;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileServiceTests
{
    [Fact]
    public void HairAndBeardSerials_AreVirtualDistinctAndStablePerMobile()
    {
        var mobiles = new MobileService();
        var aria = new Serial(0x00000002);

        var hair = mobiles.HairSerial(aria);
        var beard = mobiles.BeardSerial(aria);

        Assert.True(hair.IsVirtual);
        Assert.True(beard.IsVirtual);
        Assert.NotEqual(hair, beard);
        Assert.Equal(hair, mobiles.HairSerial(aria));
        Assert.Equal(beard, mobiles.BeardSerial(aria));
    }

    [Fact]
    public void HairSerials_OfDifferentMobiles_Differ()
    {
        var mobiles = new MobileService();

        Assert.NotEqual(mobiles.HairSerial(new Serial(2)), mobiles.HairSerial(new Serial(3)));
    }

    [Fact]
    public void FirstVirtualSerial_IsTheStartOfTheVirtualRange()
    {
        Assert.Equal(new Serial(Serial.MinVirtual), new MobileService().HairSerial(new Serial(2)));
    }

    [Fact]
    public void EnterWorld_MarksTheMobileInTheWorld()
    {
        var mobiles = new MobileService();
        var aria = new Serial(2);

        mobiles.EnterWorld(aria);

        Assert.True(mobiles.IsInWorld(aria));
        Assert.False(mobiles.IsInWorld(new Serial(3)));
        Assert.Equal([aria], mobiles.InWorld);
    }
}
