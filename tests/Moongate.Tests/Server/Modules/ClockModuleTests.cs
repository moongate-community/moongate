using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Modules;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public sealed class ClockModuleTests
{
    [Test]
    public void Describe_WhenWorldIsAtMidnight_ShouldReturnWitchingHour()
    {
        var sessionService = new FakeGameNetworkSessionService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x220,
            Character = new()
            {
                Id = (Serial)0x220,
                MapId = 0,
                Location = Point3D.Zero
            }
        };
        sessionService.Add(session);
        var module = new ClockModule(sessionService);

        var description = module.Describe((uint)session.CharacterId, "1997-09-01T00:00:00Z");

        Assert.That(description, Is.EqualTo("'Tis the witching hour."));
    }

    [Test]
    public void ExactTime_WithCustomClockConfig_ShouldUseConfiguredValues()
    {
        var sessionService = new FakeGameNetworkSessionService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x222,
            Character = new()
            {
                Id = (Serial)0x222,
                MapId = 0,
                Location = Point3D.Zero
            }
        };
        sessionService.Add(session);
        var module = new ClockModule(
            sessionService,
            null,
            new()
            {
                LightWorldStartUtc = "2000-01-01T00:00:00Z",
                LightSecondsPerUoMinute = 10
            }
        );

        var exactTime = module.ExactTime((uint)session.CharacterId, "2000-01-01T00:50:00Z");

        Assert.That(exactTime, Is.EqualTo("5:00"));
    }

    [Test]
    public void ExactTime_WithMapAndXOffset_ShouldMatchModernUoClockStyle()
    {
        var sessionService = new FakeGameNetworkSessionService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x221,
            Character = new()
            {
                Id = (Serial)0x221,
                MapId = 1,
                Location = new(4000, 0, 0)
            }
        };
        sessionService.Add(session);
        var module = new ClockModule(sessionService);

        var exactTime = module.ExactTime((uint)session.CharacterId, "1997-09-01T00:00:00Z");

        Assert.That(exactTime, Is.EqualTo("9:30"));
    }
}
