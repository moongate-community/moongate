using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class RegionAnnouncerTests
{
    private static readonly RegionContent Britain = Region("Britain", true);
    private static readonly RegionContent Field = Region("A Wheatfield in Britain 1", true, "Britain");
    private static readonly RegionContent Inn = Region(null, true, "Britain");
    private static readonly RegionContent Covetous = Region("Covetous", false, type: RegionType.Dungeon);
    private static readonly RegionContent Trinsic = Region("Trinsic", true);
    private static readonly RegionContent OtherBritain = Region("Britain", true, map: MapType.Felucca);
    private static readonly RegionContent Nameless = Region(null, true);
    private static readonly RegionContent HavenIsland = Region("Haven Island", false, type: RegionType.NoHousing);
    private static readonly RegionContent NewHaven = Region("New Haven", true, "Haven Island");
    private static readonly RegionContent Den = Region("Buccaneer's Den", false);
    private static readonly RegionContent DenHouse = Region(null, true, "Buccaneer's Den");
    private static readonly RegionContent Moongates = Region("Moongates", true, type: RegionType.Guarded);

    private readonly RecordingSpeechService _speech = new();
    private readonly MobileEntity _aria = new() { Id = new Serial(2), AccountId = new Serial(0x42), Name = "Aria" };
    private readonly RegionAnnouncer _announcer;

    public RegionAnnouncerTests()
    {
        _announcer = new(
            new StubDataLoaderService().With(
                Britain,
                Field,
                Inn,
                Covetous,
                Trinsic,
                OtherBritain,
                Nameless,
                HavenIsland,
                NewHaven,
                Den,
                DenHouse,
                Moongates
            ),
            _speech
        );
        // In the world, in the wilderness, with its login complete.
        _announcer.RegionChanged(_aria, null, null);
        _announcer.LoggedIn(_aria);
    }

    [Fact]
    public void IntoATown_ThePlayerReadsItsName_AndThatItsGuardsProtectIt()
    {
        _announcer.RegionChanged(_aria, null, Britain);

        Assert.Equal(
            ["You have entered Britain.", "You are now under the protection of the guards of Britain."],
            Told()
        );
    }

    [Fact]
    public void WhatIsEntered_IsGreen_WhatIsLeft_IsRed_TheClientsOwnTextsToo()
    {
        _announcer.RegionChanged(_aria, null, Britain);
        _announcer.RegionChanged(_aria, Britain, null);
        _announcer.RegionChanged(_aria, null, Nameless);
        _announcer.RegionChanged(_aria, Nameless, null);

        Assert.Equal(
            [RegionAnnouncer.EnterHue, RegionAnnouncer.EnterHue, RegionAnnouncer.LeaveHue, RegionAnnouncer.LeaveHue],
            _speech.ToldHues
        );
        Assert.Equal([RegionAnnouncer.EnterHue, RegionAnnouncer.LeaveHue], _speech.ToldClilocHues);
        Assert.NotEqual(RegionAnnouncer.EnterHue, RegionAnnouncer.LeaveHue);
    }

    [Fact]
    public void OutOfATown_ThePlayerReadsThatItLeftItsGuards_AndTheTown()
    {
        _announcer.RegionChanged(_aria, Britain, null);

        Assert.Equal(["You have left the protection of the guards of Britain.", "You have left Britain."], Told());
    }

    [Fact]
    public void InsideATown_FromOnePartToAnother_NothingIsSaid()
    {
        _announcer.RegionChanged(_aria, Britain, Field);
        _announcer.RegionChanged(_aria, Field, Inn);
        _announcer.RegionChanged(_aria, Inn, Britain);

        Assert.Empty(Told());
        Assert.Empty(_speech.ToldClilocs);
    }

    [Fact]
    public void FromAPartOfATown_ToTheWilderness_TheTownIsNamed_NotThePart()
    {
        _announcer.RegionChanged(_aria, Field, null);

        Assert.Equal(["You have left the protection of the guards of Britain.", "You have left Britain."], Told());
    }

    [Fact]
    public void FromATownToAPlaceWithNoGuards_BothAreNamed()
    {
        _announcer.RegionChanged(_aria, Britain, Covetous);

        Assert.Equal(
            [
                "You have left the protection of the guards of Britain.", "You have left Britain.",
                "You have entered Covetous."
            ],
            Told()
        );
    }

    [Fact]
    public void FromOneTownToAnother_TheGuardsChangeToo()
    {
        _announcer.RegionChanged(_aria, Britain, Trinsic);

        Assert.Equal(
            [
                "You have left the protection of the guards of Britain.", "You have left Britain.",
                "You have entered Trinsic.", "You are now under the protection of the guards of Trinsic."
            ],
            Told()
        );
    }

    [Fact]
    public void TheSameTownOnAnotherMap_IsTheSameName_SoNothingIsSaid()
    {
        _announcer.RegionChanged(_aria, Britain, OtherBritain);

        Assert.Empty(Told());
    }

    [Fact]
    public void AGuardedPlaceWithNoName_UsesTheClientsOwnTexts()
    {
        _announcer.RegionChanged(_aria, null, Nameless);
        _announcer.RegionChanged(_aria, Nameless, null);

        Assert.Empty(Told());
        Assert.Equal([500112, 500113], _speech.ToldClilocs.Select(told => told.Cliloc));
    }

    [Fact]
    public void ATownInsideAnIsland_IsAPlaceOfItsOwn_AndItsGuardsBearItsName()
    {
        // New Haven is a guarded town inside Haven Island, which has no guards.
        _announcer.RegionChanged(_aria, HavenIsland, NewHaven);

        Assert.Equal(
            ["You have entered New Haven.", "You are now under the protection of the guards of New Haven."],
            Told()
        );

        _speech.Told.Clear();
        _announcer.RegionChanged(_aria, NewHaven, HavenIsland);

        // Still on the island: only the town is left.
        Assert.Equal(["You have left the protection of the guards of New Haven.", "You have left New Haven."], Told());
    }

    [Fact]
    public void StraightIntoATownInsideAnIsland_BothAreEntered_TheOuterFirst()
    {
        _announcer.RegionChanged(_aria, null, NewHaven);

        Assert.Equal(
            [
                "You have entered Haven Island.", "You have entered New Haven.",
                "You are now under the protection of the guards of New Haven."
            ],
            Told()
        );
    }

    [Fact]
    public void AGuardedHouseWithNoNameInATownWithoutGuards_DoesNotLendTheTownItsGuards()
    {
        _announcer.RegionChanged(_aria, Den, DenHouse);
        _announcer.RegionChanged(_aria, DenHouse, Den);

        Assert.Empty(Told());
        Assert.Equal([500112, 500113], _speech.ToldClilocs.Select(told => told.Cliloc));
    }

    [Fact]
    public void AGuardedSpotThatIsNotAPlace_SuchAsTheMoongates_IsNotNamed()
    {
        _announcer.RegionChanged(_aria, null, Moongates);

        Assert.Empty(Told());
        Assert.Equal(500112, Assert.Single(_speech.ToldClilocs).Cliloc);
    }

    [Fact]
    public void BeforeItsLoginIsComplete_APlayerIsToldNothing_ThenWhereItIs()
    {
        var bran = new MobileEntity { Id = new Serial(3), AccountId = new Serial(0x43), Name = "Bran" };

        // Entering the world comes before the client is in game.
        _announcer.RegionChanged(bran, null, Britain);
        Assert.Empty(Told());

        _announcer.LoggedIn(bran);
        Assert.Equal(
            ["You have entered Britain.", "You are now under the protection of the guards of Britain."],
            Told()
        );

        // Gone and back: the next login waits again.
        _announcer.Left(bran.Id);
        _speech.Told.Clear();
        _announcer.RegionChanged(bran, null, Trinsic);
        Assert.Empty(Told());
    }

    [Fact]
    public async Task TheLoginEvent_IsWhatCompletesALogin()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        using var container = new Container();
        container.RegisterMoongateEventBus();
        var events = container.Resolve<IMoongateEventBus>();
        var announcer = new RegionAnnouncer(
            new StubDataLoaderService().With(Britain),
            _speech,
            events,
            fixture.Network.Loop
        );
        var bran = new MobileEntity { Id = new Serial(3), AccountId = new Serial(0x43), Name = "Bran" };
        await announcer.StartAsync();
        await fixture.Network.ExecuteOnLoopAsync(() => announcer.RegionChanged(bran, null, Britain));

        await events.PublishAsync(new CharacterEnteredWorldEvent(bran));

        Assert.Equal("You have entered Britain.", _speech.Told[0].Text);
        await announcer.StopAsync();
    }

    private string[] Told()
    {
        return _speech.Told.Select(told => told.Text).ToArray();
    }

    private static RegionContent Region(
        string? name,
        bool guarded,
        string? parent = null,
        MapType map = MapType.Trammel,
        RegionType type = RegionType.Town
    )
    {
        return new() { Map = map, Name = name, Guarded = guarded, Parent = parent, Type = type };
    }
}
