using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Subscribers;

namespace Moongate.Tests.Server.Subscribers;

public class PaperdollSubscriberTests
{
    private static MobileEntity Humanoid()
        => new() { Id = new Serial(0x64), Name = "Hero", Body = 400 };

    [Fact]
    public void Build_HumanoidBeheld_ProducesThePaperdoll()
    {
        var packet = PaperdollSubscriber.Build(Humanoid(), new Serial(0x99));

        Assert.NotNull(packet);
        Assert.Equal(0x64u, packet!.Value.Serial.Value);
        Assert.Equal("Hero", packet.Value.Text);
    }

    [Fact]
    public void Build_NonHumanoidBeheld_ProducesNothing()
    {
        var horse = Humanoid();
        horse.Body = 200;

        Assert.Null(PaperdollSubscriber.Build(horse, new Serial(0x99)));
    }

    [Fact]
    public void Build_OwnCharacter_AllowsLifting()
    {
        var mobile = Humanoid();

        Assert.True(PaperdollSubscriber.Build(mobile, mobile.Id)!.Value.CanLift);
    }

    [Fact]
    public void Build_SomeoneElse_DoesNotAllowLifting()
        => Assert.False(PaperdollSubscriber.Build(Humanoid(), new Serial(0x99))!.Value.CanLift);

    [Fact]
    public void Build_NoBeholderCharacter_DoesNotAllowLifting()
        => Assert.False(PaperdollSubscriber.Build(Humanoid(), null)!.Value.CanLift);

    [Fact]
    public void Build_CarriesWarmodeFromTheBeheld()
    {
        var mobile = Humanoid();
        mobile.Warmode = true;

        Assert.True(PaperdollSubscriber.Build(mobile, null)!.Value.Warmode);
    }
}
