using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpeechServiceTests
{
    [Fact]
    public async Task Say_ReachesThePlayersWithin15CellsOnTheSameMapOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var near = await fixture.AddAsync(2);
        var far = await fixture.AddAsync(3);
        var elsewhere = await fixture.AddAsync(4, map: MapType.Felucca);
        Place(fixture, 2, 115, 100);
        Place(fixture, 3, 116, 100);
        Place(fixture, 4, 100, 100);
        var orc = new MobileEntity
        {
            Id = new Serial(0x100), Name = "an orc", Body = 0x11, Map = MapType.Trammel, Location = new Point3D(100, 100, 0)
        };
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = speech.Say(orc, "Grr"));

        Assert.Equal(1, sent);
        Assert.Equal([near.SessionId], fixture.Sender.SentSessionIds);
        Assert.DoesNotContain(far.SessionId, fixture.Sender.SentSessionIds);
        Assert.DoesNotContain(elsewhere.SessionId, fixture.Sender.SentSessionIds);
        var message = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Equal(("Grr", "an orc", SpeechType.Regular), (message.Text, message.Name, message.Type));
    }

    [Fact]
    public async Task SayCliloc_ReachesThePlayersWhoHearTheSpeaker_AsATextOfTheClient()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var near = await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        Place(fixture, 2, 115, 100);
        Place(fixture, 3, 116, 100);
        var banker = new MobileEntity
        {
            Id = new Serial(0x100), Name = "Bank Teller", Body = 0x0190, Map = MapType.Trammel, Location = new Point3D(100, 100, 0)
        };
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = speech.SayCliloc(banker, 1042759, "1,200"));

        Assert.Equal(1, sent);
        Assert.Equal([near.SessionId], fixture.Sender.SentSessionIds);
        var message = Assert.IsType<LocalizedMessagePacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Equal((banker.Id, 0x0190, 1042759, "Bank Teller", "1,200"), (message.Serial, message.Graphic, message.Cliloc, message.Name, message.Arguments));
    }

    [Fact]
    public async Task Tell_SendsASystemMessageToThatPlayerOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var aria = await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        Assert.True(fixture.Mobiles.TryGet(new Serial(2), out var mobile));
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var told = false;

        await fixture.Network.ExecuteOnLoopAsync(() => told = speech.Tell(mobile!, "That is too far away."));

        Assert.True(told);
        Assert.Equal([aria.SessionId], fixture.Sender.SentSessionIds);
        var message = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Equal(("That is too far away.", SpeechType.System), (message.Text, message.Type));
    }

    [Fact]
    public async Task Tell_AndTellCliloc_WithAHue_SendTheTextInThatColour()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        Assert.True(fixture.Mobiles.TryGet(new Serial(2), out var mobile));
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);

        await fixture.Network.ExecuteOnLoopAsync(() =>
            {
                speech.Tell(mobile!, "You have entered Britain.", 0x3F);
                speech.TellCliloc(mobile!, 500113, "", 0x22);
            }
        );

        Assert.Equal(0x3F, Assert.IsType<UnicodeSpeechMessagePacket>(fixture.Sender.Sent[0]).Hue.Value);
        Assert.Equal(0x22, Assert.IsType<LocalizedMessagePacket>(fixture.Sender.Sent[1]).Hue);
    }

    [Fact]
    public async Task Tell_AnNpc_IsFalseAndSendsNothing()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(2);
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "an orc", Map = MapType.Trammel, Location = new Point3D(100, 100, 0) };
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var told = true;

        await fixture.Network.ExecuteOnLoopAsync(() => told = speech.Tell(orc, "Grr"));

        Assert.False(told);
        Assert.Empty(fixture.Sender.Sent);
    }

    [Fact]
    public async Task PlaySound_ReachesThePlayersWithin15CellsOnTheSameMapOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var near = await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        Place(fixture, 2, 110, 100);
        Place(fixture, 3, 116, 100);
        var cat = new MobileEntity
        {
            Id = new Serial(0x100), Name = "Vega", Body = 0xC9, Map = MapType.Trammel, Location = new Point3D(100, 100, 0)
        };
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(() => sent = speech.PlaySound(cat, 0x69));

        Assert.Equal(1, sent);
        Assert.Equal([near.SessionId], fixture.Sender.SentSessionIds);
        var sound = Assert.IsType<PlaySoundPacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Equal((0x69, cat.Location), (sound.Sound, sound.Location));
    }

    [Fact]
    public async Task PlaySound_AtALocation_ReachesThePlayersWithin15Cells()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var near = await fixture.AddAsync(2);
        await fixture.AddAsync(3);
        Place(fixture, 2, 110, 100);
        Place(fixture, 3, 116, 100);
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);

        await fixture.Network.ExecuteOnLoopAsync(() => speech.PlaySound(MapType.Trammel, new Point3D(100, 100, 5), 0xEA));

        Assert.Equal([near.SessionId], fixture.Sender.SentSessionIds);
        var sound = Assert.IsType<PlaySoundPacket>(Assert.Single(fixture.Sender.Sent));
        Assert.Equal((0xEA, new Point3D(100, 100, 5)), (sound.Sound, sound.Location));
    }

    [Fact]
    public async Task Say_OfAHiddenMobile_ReachesItselfAndTheStaffOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var player = await fixture.AddAsync(2);
        var staff = await fixture.AddAsync(3);
        var own = await fixture.AddAsync(4);
        Place(fixture, 2, 101, 100);
        Place(fixture, 3, 102, 100);
        Place(fixture, 4, 100, 100);
        Assert.True(fixture.Mobiles.TryGet(new Serial(4), out var speaker));
        speaker.Hidden = true;
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(
            () =>
            {
                staff.Set(SessionKeys.AccountType, AccountType.GameMaster);
                sent = speech.Say(speaker, "psst");
            }
        );

        Assert.Equal(2, sent);
        Assert.Contains(staff.SessionId, fixture.Sender.SentSessionIds);
        Assert.Contains(own.SessionId, fixture.Sender.SentSessionIds);
        Assert.DoesNotContain(player.SessionId, fixture.Sender.SentSessionIds);
    }

    [Fact]
    public async Task PlaySound_OfAHiddenMobile_ReachesItselfAndTheStaffOnly()
    {
        await using var fixture = await BroadcastFixture.CreateAsync();
        var player = await fixture.AddAsync(2);
        var staff = await fixture.AddAsync(3);
        var own = await fixture.AddAsync(4);
        Place(fixture, 2, 101, 100);
        Place(fixture, 3, 102, 100);
        Place(fixture, 4, 100, 100);
        Assert.True(fixture.Mobiles.TryGet(new Serial(4), out var source));
        source.Hidden = true;
        var speech = new SpeechService(fixture.Sessions, fixture.Mobiles, fixture.Sender);
        var sent = 0;

        await fixture.Network.ExecuteOnLoopAsync(
            () =>
            {
                staff.Set(SessionKeys.AccountType, AccountType.GameMaster);
                sent = speech.PlaySound(source, 0x69);
            }
        );

        Assert.Equal(2, sent);
        Assert.DoesNotContain(player.SessionId, fixture.Sender.SentSessionIds);
        Assert.Contains(staff.SessionId, fixture.Sender.SentSessionIds);
        Assert.Contains(own.SessionId, fixture.Sender.SentSessionIds);
    }

    private static void Place(BroadcastFixture fixture, uint serial, int x, int y)
    {
        Assert.True(fixture.Mobiles.TryGet(new Serial(serial), out var mobile));
        mobile.Location = new Point3D(x, y, 0);
    }
}
