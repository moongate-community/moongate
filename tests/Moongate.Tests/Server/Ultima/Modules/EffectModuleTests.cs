using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class EffectModuleTests
{
    private const long ChestSerial = 0x40000010;
    private const long PackedSerial = 0x40000011;

    private readonly RecordingEffectService _effects = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", Map = MapType.Felucca, Location = new Point3D(1601, 1600, 5)
    };

    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(3), Name = "an orc", Map = MapType.Felucca, Location = new Point3D(1610, 1600, 0)
    };

    public EffectModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
        var chest = new ItemEntity { Id = new((uint)ChestSerial), TemplateId = "chest", ItemId = 0x0E41, Amount = 1 };
        chest.PlaceOnGround(MapType.Felucca, new Point3D(1605, 1602, 0));
        var packed = new ItemEntity { Id = new((uint)PackedSerial), TemplateId = "coin", ItemId = 0x0EED, Amount = 1 };
        packed.PutInContainer(chest.Id, new Point2D(10, 10));
        _items.Add([chest, packed]);
    }

    [Fact]
    public void At_PlaysTheGraphicAtThePointWithTheDefaults()
    {
        var result = Run("return effect.at(1, 1600, 1628, 5, 0x3728)");

        Assert.True(result[0].Read<bool>());
        var (map, location, options) = Assert.Single(_effects.At);
        Assert.Equal((MapType.Trammel, new Point3D(1600, 1628, 5)), (map, location));
        Assert.Equal(new EffectOptions { Graphic = 0x3728 }, options);
    }

    [Fact]
    public void At_ReadsEveryOption()
    {
        Run(
            """
            return effect.at(1, 1600, 1628, 5, 0x376A, {
                speed = 9, duration = 32, hue = 0x47F, render = 4, fixed_direction = true, explodes = true,
                particle = 5005, explode_particle = 4019, explode_sound = 0x160, layer = 3
            })
            """
        );

        Assert.Equal(
            new EffectOptions
            {
                Graphic = 0x376A, Speed = 9, Duration = 32, Hue = new Hue(0x47F),
                RenderMode = EffectRenderModeType.Translucent,
                FixedDirection = true, Explodes = true, Particle = 5005, ExplodeParticle = 4019, ExplodeSound = 0x160,
                Layer = EffectLayerType.Waist
            },
            Assert.Single(_effects.At).Options
        );
    }

    [Theory,
     InlineData("return effect.at(99, 1600, 1628, 5, 0x3728)"),
     InlineData("return effect.at(1, 1600, 1628, 200, 0x3728)"),
     InlineData("return effect.at(1, -1, 1628, 5, 0x3728)"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x10000)"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { speed = 300 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { render = 9 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { layer = 6 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { speed = '9' })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { speed = 9.5 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { explodes = 1 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { fixed_direction = 'true' })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0x3728, { spede = 3 })"),
     InlineData("return effect.at(1, 1600, 1628, 5, 0)")]
    public void At_AValueOutOfRange_IsFalseAndPlaysNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_effects.At);
    }

    [Fact]
    public void At_ParticlesWithoutAGraphic_ArePlayed()
    {
        Assert.True(Run("return effect.at(1, 1600, 1628, 5, 0, { particle = 2023 })")[0].Read<bool>());

        Assert.Equal(2023, Assert.Single(_effects.At).Options.Particle);
    }

    [Fact]
    public void On_AMobile_PlaysOnItWhereItStands()
    {
        Assert.True(Run("return effect.on(2, 0x376A, { speed = 9 })")[0].Read<bool>());

        var (target, map, location, options) = Assert.Single(_effects.On);
        Assert.Equal((_aria.Id, MapType.Felucca, _aria.Location, (byte)9), (target, map, location, options.Speed));
    }

    [Fact]
    public void On_AGroundItem_PlaysOnItWhereItLies()
    {
        Assert.True(Run($"return effect.on({ChestSerial}, 0x3728)")[0].Read<bool>());

        var (target, map, location, _) = Assert.Single(_effects.On);
        Assert.Equal((new Serial((uint)ChestSerial), MapType.Felucca, new Point3D(1605, 1602, 0)), (target, map, location));
    }

    [Theory, InlineData("999"), InlineData("-1"), InlineData("0x40000011")]
    public void On_SomethingNotInTheWorld_IsFalse(string serial)
    {
        // An unknown serial, or an item inside a container: it has no place of its own in the world.
        Assert.False(Run($"return effect.on({serial}, 0x3728)")[0].Read<bool>());

        Assert.Empty(_effects.On);
    }

    [Fact]
    public void Moving_FliesFromOneObjectToTheOther()
    {
        Assert.True(Run("return effect.moving(2, 3, 0x36D4, { speed = 7, duration = 0, explodes = true })")[0].Read<bool>());

        var moving = Assert.Single(_effects.Moving);
        Assert.Equal(
            (MapType.Felucca, _aria.Id, _aria.Location, _orc.Id, _orc.Location, true),
            (moving.Map, moving.Source, moving.From, moving.Target, moving.To, moving.Options.Explodes)
        );
    }

    [Fact]
    public void Moving_BetweenTwoMaps_IsFalse()
    {
        _orc.Map = MapType.Trammel;

        Assert.False(Run("return effect.moving(2, 3, 0x36D4)")[0].Read<bool>());
        Assert.Empty(_effects.Moving);
    }

    [Fact]
    public void Lightning_StrikesTheObject()
    {
        Assert.True(Run("return effect.lightning(3)")[0].Read<bool>());
        Assert.True(Run("return effect.lightning(3, 0x21)")[0].Read<bool>());
        Assert.False(Run("return effect.lightning(999)")[0].Read<bool>());

        Assert.Equal(
            [
                (_orc.Id, MapType.Felucca, _orc.Location, new Hue(0)),
                (_orc.Id, MapType.Felucca, _orc.Location, new Hue(0x21))
            ],
            _effects.Lightning
        );
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new EffectModule(_effects, _mobiles, _items));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
