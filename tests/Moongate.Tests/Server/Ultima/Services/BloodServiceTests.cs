using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Timing;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Maps;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Moongate.Core.Types.Geometry;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class BloodServiceTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly ScriptedRandom _random = new();
    private readonly ManualTimeProvider _time = new();
    private readonly CombatConfig _config = new();
    private readonly FakeTileDataService _tiles = new();
    private readonly FakeMapService _maps = new(64, 64);
    private ItemTemplateService _itemTemplates = null!;
    private MobileTemplateService _mobileTemplates = null!;
    private BroadcastFixture _fixture = null!;
    private MobileEntity _orc = null!;
    private BloodService _blood = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _itemTemplates = new(
            new StubDataLoaderService().With(
                Splash("blood_splash_0x1645", 0x1645),
                Splash("blood_splash_0x122a", 0x122A),
                Splash("blood_splash_0x122b", 0x122B)
            )
        );
        _mobileTemplates = new(
            new StubDataLoaderService().With(
                new MobileTemplate { Id = "orc" },
                new MobileTemplate { Id = "skeleton", BloodHue = -1 },
                new MobileTemplate { Id = "slime", BloodHue = 0x0044 }
            )
        );
        _orc = new MobileEntity
        {
            Id = new Serial(900), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = new Point3D(7, 8, 5)
        };

        for (var i = 0; i < 30; i++)
        {
            _serials.Serials.Enqueue(new Serial(0x40000100 + (uint)i));
        }

        _blood = new BloodService(
            _config,
            _mobileTemplates,
            new ItemHandlingService(
                _items,
                _fixture.Sessions,
                _fixture.Sender,
                _view,
                TestTooltips.Create(_items, _fixture.Mobiles),
                new FakeItemFactoryService(_itemTemplates, _tiles),
                _serials
            ),
            _items,
            _serials,
            _view,
            _time,
            _random,
            _maps
        );
    }

    [Fact]
    public void Splash_ALoneBlood_LiesUnderTheVictim_AndShowsToThePlayers()
    {
        _config.BloodPieces = 0;
        _random.Integers(0);

        _blood.Splash(_orc);

        var piece = Assert.Single(Pieces());
        Assert.Equal(("blood_splash_0x1645", (MapType?)MapType.Felucca, (Point3D?)new Point3D(7, 8, 5)), (piece.TemplateId, piece.Map, piece.GroundLocation));
        Assert.Contains($"Appeared {piece.Id.Value}", _view.Calls);
    }

    [Fact]
    public void Splash_TheOnesAround_AreFromOneToTheMostPieces_WithinOneTileOfTheVictim()
    {
        _config.BloodPieces = 2;

        // The graphic of the first piece, how many around (1 + 1), then for each of them its offsets and its graphic.
        _random.Integers(0, 1, 2, 0, 0, 0, 2, 1);

        _blood.Splash(_orc);

        var pieces = Pieces();
        Assert.Equal(
            [new Point3D(7, 8, 5), new Point3D(8, 7, 5), new Point3D(6, 9, 5)],
            pieces.Select(piece => piece.GroundLocation!.Value)
        );
    }

    [Fact]
    public void Splash_EveryPiece_DecaysAfterTheSecondsOfTheConfig_WithTheHueOfTheVictim()
    {
        _config.BloodPieces = 0;
        _config.BloodSeconds = 7;
        var slime = new MobileEntity { Id = new Serial(901), TemplateId = "slime", Map = MapType.Felucca, Location = new Point3D(1, 2, 0) };
        var now = _time.GetUtcNow().UtcDateTime;

        _blood.Splash(slime);

        var piece = Assert.Single(Pieces());
        Assert.InRange(piece.DecayAt!.Value, now.AddSeconds(7), now.AddSeconds(8));
        Assert.Equal(0x0044, piece.Hue.Value);
    }

    [Fact]
    public void Splash_AMobileWithoutBlood_AMobileWhenItIsOff_LeavesNothing()
    {
        var skeleton = new MobileEntity { Id = new Serial(902), TemplateId = "skeleton", Map = MapType.Felucca, Location = new Point3D(3, 3, 0) };

        _blood.Splash(skeleton);
        _config.BloodEnabled = false;
        _blood.Splash(_orc);

        Assert.Empty(Pieces());
    }

    [Fact]
    public void Splash_APlayerOrAnUnknownTemplate_BleedsRed()
    {
        _config.BloodPieces = 0;
        var player = new MobileEntity { Id = new Serial(903), Map = MapType.Felucca, Location = new Point3D(3, 3, 0) };

        _blood.Splash(player);

        Assert.Equal(0, Assert.Single(Pieces()).Hue.Value);
    }

    [Fact]
    public void Splash_WhenTheSerialPoolRunsShort_LeavesTheSerialsToTheLoot()
    {
        while (_serials.Serials.Count > ItemSerialPool.RefillBelow)
        {
            _serials.Serials.Dequeue();
        }

        _blood.Splash(_orc);

        Assert.Empty(Pieces());
    }

    [Fact]
    public void Splash_APieceOffTheMap_IsNotLeft()
    {
        _config.BloodPieces = 1;
        var edge = new MobileEntity { Id = new Serial(904), Map = MapType.Felucca, Location = new Point3D(0, 0, 0) };

        // The first piece, how many around (1 + 0), then the offsets -1 and -1 of the one that would lie off the map.
        _random.Integers(0, 0, 0, 0, 0);
        _blood.Splash(edge);

        Assert.Single(Pieces());
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private static ItemTemplate Splash(string id, int graphic)
    {
        return new ItemTemplate { Id = id, ItemId = new Serial((uint)graphic) };
    }

    private List<ItemEntity> Pieces()
    {
        var pieces = new List<ItemEntity>();

        for (var i = 0; i < 30; i++)
        {
            if (_items.TryGet(new Serial(0x40000100 + (uint)i), out var item))
            {
                pieces.Add(item);
            }
        }

        return pieces;
    }
}
