using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
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

    private static void Place(BroadcastFixture fixture, uint serial, int x, int y)
    {
        Assert.True(fixture.Mobiles.TryGet(new Serial(serial), out var mobile));
        mobile.Location = new Point3D(x, y, 0);
    }
}
