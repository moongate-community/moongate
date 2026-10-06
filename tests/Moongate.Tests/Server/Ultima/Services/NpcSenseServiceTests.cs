using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcSenseServiceTests
{
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingNpcScriptService _scripts = new();
    private readonly NpcSenseService _senses;

    public NpcSenseServiceTests()
    {
        _senses = new(_scripts, _sectors, new NpcsConfig { SenseRange = 8 });
    }

    [Fact]
    public void Moved_APlayerComingWithinRange_IsSensedByTheNpc()
    {
        Add(Npc(0x100, 1600, 1600));
        var aria = Add(Player(2, 1609, 1600));

        Step(aria, 1608, 1600);

        Assert.Equal(["Queue 256 on_mobile_in_range 2"], _scripts.Calls);
    }

    [Theory, InlineData(1607, 1606), InlineData(1608, 1609), InlineData(1620, 1619)]
    public void Moved_APlayerAlreadyInRangeLeavingOrFar_IsNotSensed(int fromX, int toX)
    {
        Add(Npc(0x100, 1600, 1600));
        var aria = Add(Player(2, fromX, 1600));

        Step(aria, toX, 1600);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public void Moved_ACornerOfTheSquareRange_IsAlreadyInRange()
    {
        // The range is a square along X and Y, as the sectors and the view range.
        Add(Npc(0x100, 1600, 1600));
        var aria = Add(Player(2, 1608, 1608));

        Step(aria, 1607, 1608);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public void Moved_AnNpcComingWithinRangeOfAPlayer_SensesThePlayer()
    {
        var orc = Add(Npc(0x100, 1600, 1600));
        Add(Player(2, 1609, 1600));

        Step(orc, 1601, 1600);

        Assert.Equal(["Queue 256 on_mobile_in_range 2"], _scripts.Calls);
    }

    [Fact]
    public void Moved_TwoNpcsComingWithinRange_SenseEachOther()
    {
        var orc = Add(Npc(0x100, 1600, 1600));
        Add(Npc(0x101, 1609, 1600));

        Step(orc, 1601, 1600);

        Assert.Equal(["Queue 256 on_mobile_in_range 257", "Queue 257 on_mobile_in_range 256"], _scripts.Calls.Order());
    }

    [Fact]
    public void Moved_AcrossMapsOrOnTheSpot_IsNotSensed()
    {
        Add(Npc(0x100, 1600, 1600, MapType.Felucca));
        var aria = Add(Player(2, 1609, 1600));

        Step(aria, 1608, 1600);
        _senses.Moved(aria, aria.Location);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public void Appeared_APlayerNearAnNpc_IsSensedByIt()
    {
        Add(Npc(0x100, 1600, 1600));
        Add(Npc(0x101, 1600, 1620));

        _senses.Appeared(Add(Player(2, 1608, 1592)));

        Assert.Equal(["Queue 256 on_mobile_in_range 2"], _scripts.Calls);
    }

    [Fact]
    public void Appeared_AnNpc_SensesWhoIsAroundAndIsSensedByTheNpcs()
    {
        Add(Player(2, 1605, 1600));
        Add(Npc(0x101, 1595, 1600));
        var orc = Add(Npc(0x100, 1600, 1600));

        _senses.Appeared(orc);

        Assert.Equal(
            ["Queue 256 on_mobile_in_range 2", "Queue 256 on_mobile_in_range 257", "Queue 257 on_mobile_in_range 256"],
            _scripts.Calls.Order()
        );
    }

    [Fact]
    public void Moved_AHiddenPlayerComingWithinRange_IsNotSensed()
    {
        Add(Npc(0x100, 1600, 1600));
        var aria = Add(Player(2, 1609, 1600));
        aria.Hidden = true;

        Step(aria, 1608, 1600);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public void Moved_AGhostInWarModeComingWithinRange_IsNotSensed()
    {
        Add(Npc(0x100, 1600, 1600));
        var aria = Add(Player(2, 1609, 1600));
        aria.Body = 0x0192;

        Step(aria, 1608, 1600);

        Assert.Empty(_scripts.Calls);
    }

    [Fact]
    public void Appeared_AnNpcNearAHiddenPlayer_DoesNotSenseIt()
    {
        var aria = Add(Player(2, 1601, 1600));
        aria.Hidden = true;

        _senses.Appeared(Add(Npc(0x100, 1600, 1600)));

        Assert.Empty(_scripts.Calls);
    }

    private MobileEntity Add(MobileEntity mobile)
    {
        _sectors.Add(mobile);

        return mobile;
    }

    private void Step(MobileEntity mobile, int x, int y)
    {
        var old = mobile.Location;
        mobile.Location = new Point3D(x, y, 0);
        _sectors.Move(mobile);
        _senses.Moved(mobile, old);
    }

    private static MobileEntity Npc(uint serial, int x, int y, MapType map = MapType.Trammel)
    {
        return new() { Id = new Serial(serial), Name = "npc", TemplateId = "orc", Map = map, Location = new Point3D(x, y, 0) };
    }

    private static MobileEntity Player(uint serial, int x, int y)
    {
        var player = Npc(serial, x, y);
        player.AccountId = new Serial(0x42);

        return player;
    }
}
