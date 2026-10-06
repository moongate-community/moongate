using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Combat;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;
using Moongate.Core.Types.Geometry;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class AmmoServiceTests : IAsyncLifetime
{
    private static readonly WeaponInfo Bow = new(SkillType.Archery, WeaponType.Bow, true, 9, 41, 25);
    private static readonly WeaponInfo Crossbow = new(SkillType.Archery, WeaponType.Crossbow, true, 9, 41, 25);

    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubItemSerialPool _serials = new();
    private readonly StubCombatGearService _gear = new();
    private readonly ScriptedRandom _random = new();
    private readonly FakeTileDataService _tiles = new FakeTileDataService().Item(0x0F3F, TileFlagType.Generic, 0).Item(0x1BFB, TileFlagType.Generic, 0);
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "0x0f3f_arrow", ItemId = new Serial(0x0F3F), Stackable = true },
            new ItemTemplate { Id = "0x1bfb_crossbow_bolt", ItemId = new Serial(0x1BFB), Stackable = true }
        )
    );

    private BroadcastFixture _fixture = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _orc = null!;
    private ItemEntity _arrows = null!;
    private AmmoService _ammo = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _orc = new MobileEntity
        {
            Id = new Serial(900), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel, Location = new Point3D(7, 8, 0)
        };
        _arrows = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "0x0f3f_arrow", ItemId = 0x0F3F, Amount = 3 };
        _arrows.PutInContainer(new Serial(0x40000001), new Point2D(10, 10));
        _items.Add([_arrows]);
        _gear.Ammo = _arrows;
        _ammo = new(
            _gear,
            new ItemHandlingService(
                _items,
                _fixture.Sessions,
                _fixture.Sender,
                _view,
                TestTooltips.Create(_items, _fixture.Mobiles),
                new FakeItemFactoryService(_templates, _tiles),
                _serials
            ),
            _items,
            _view,
            _random
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Spend_TakesOneOffTheStack_AndTheLastOneEmptiesIt()
    {
        Assert.True(_ammo.Spend(_aria, Bow));
        Assert.Equal(2, _arrows.Amount);

        Assert.True(_ammo.Spend(_aria, Bow));
        Assert.True(_ammo.Spend(_aria, Bow));

        Assert.False(_items.TryGet(_arrows.Id, out _));
    }

    [Fact]
    public void Spend_WithNoAmmunition_TakesNothing()
    {
        _gear.Ammo = null;

        Assert.False(_ammo.Spend(_aria, Bow));
    }

    [Theory]
    [InlineData(39, true)]
    [InlineData(40, false)]
    public void Recover_AnArrowLiesAtTheTargetsFeet_FortyShotsInAHundred(int roll, bool found)
    {
        _random.Integers(roll);
        _serials.Serials.Enqueue(new Serial(0x40000020));

        _ammo.Recover(_orc, Bow);

        Assert.Equal(found, _items.TryGet(new Serial(0x40000020), out var arrow));

        if (found)
        {
            Assert.Equal(("0x0f3f_arrow", 1, (MapType?)MapType.Trammel, (Point3D?)new Point3D(7, 8, 0)), (arrow!.TemplateId, arrow.Amount, arrow.Map, arrow.GroundLocation));
            Assert.Contains($"Appeared {arrow.Id.Value}", _view.Calls);
        }
    }

    [Fact]
    public void Recover_ABoltOfACrossbow_IsABolt_AndAMeleeWeaponLosesNothing()
    {
        _random.Integers(0, 0);
        _serials.Serials.Enqueue(new Serial(0x40000020));

        _ammo.Recover(_orc, new WeaponInfo(SkillType.Swordsmanship, WeaponType.Sword, false, 5, 33, 35));
        Assert.False(_items.TryGet(new Serial(0x40000020), out _));

        _ammo.Recover(_orc, Crossbow);
        Assert.True(_items.TryGet(new Serial(0x40000020), out var bolt));
        Assert.Equal("0x1bfb_crossbow_bolt", bolt.TemplateId);
    }
}
