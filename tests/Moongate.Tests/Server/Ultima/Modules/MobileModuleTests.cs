using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MobileModuleTests
{
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();
    private readonly StubMusicService _music = new();
    private readonly RecordingLightService _light = new();
    private readonly RegionService _regions = new(
        new StubDataLoaderService().With(
            new RegionContent
            {
                Map = MapType.Felucca, Name = "Britain", Areas = [new RegionAreaContent { X1 = 1400, Y1 = 1500, X2 = 1700, Y2 = 1800 }]
            }
        )
    );
    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca, Location = new Point3D(3000, 3000, 0),
        Body = 17, Strength = 96, Hits = 50, HitsMax = 58, Direction = DirectionType.West
    };
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(1601, 1600, 5)
    };

    public MobileModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
        var backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([backpack]);
    }

    [Fact]
    public void NameIsPlayerAndDirection_DescribeTheMobile()
    {
        var result = Run(
            "return mobile.name(2), mobile.is_player(2), mobile.is_player(256), mobile.direction(256), mobile.name(999), mobile.is_player(999), mobile.direction(999)"
        );

        Assert.Equal("Aria", result[0].Read<string>());
        Assert.True(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
        Assert.Equal((int)DirectionType.West, result[3].Read<int>());
        Assert.Equal(LuaValue.Nil, result[4]);
        Assert.False(result[5].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[6]);
    }

    [Fact]
    public void Stats_GivesTheMobilesNumbers()
    {
        var result = Run("local stats = mobile.stats(256) return stats.body, stats.strength, stats.hits, stats.hits_max, mobile.stats(999)");

        Assert.Equal([17, 96, 50, 58], result[..4].Select(value => value.Read<int>()));
        Assert.Equal(LuaValue.Nil, result[4]);
    }

    [Fact]
    public void BackpackRegionAndLight_ComeFromTheWorld()
    {
        var result = Run(
            "return mobile.backpack(2), mobile.backpack(256), mobile.region(2), mobile.region(256), mobile.light(2), mobile.light(999)"
        );

        Assert.Equal(0x40000001, result[0].Read<long>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal("Britain", result[2].Read<string>());
        Assert.Equal(LuaValue.Nil, result[3]);
        Assert.Equal(_light.LevelFor(_aria), result[4].Read<int>());
        Assert.Equal(LuaValue.Nil, result[5]);
    }

    [Fact]
    public void PlayMusic_PlaysItToAPlayerOnly()
    {
        var result = Run("return mobile.play_music(2, 'Britain1'), mobile.play_music(256, 'Britain1'), mobile.play_music(999, 'Britain1')");

        Assert.True(result[0].Read<bool>());
        Assert.False(result[1].Read<bool>());
        Assert.False(result[2].Read<bool>());
    }

    [Fact]
    public void Props_AreKeptOnPlayersAndNpcs()
    {
        var result = Run(
            "return mobile.set_prop(2, 'quest.step', 2), mobile.get_prop(2, 'quest.step'), mobile.set_prop(256, 'angry', true), " +
            "mobile.get_prop(256, 'angry'), mobile.get_prop(2, 'nothing')"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal(2, result[1].Read<int>());
        Assert.True(result[2].Read<bool>());
        Assert.True(result[3].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[4]);
        Assert.Equal(2L, _aria.Props!["quest.step"]);
    }

    [Fact]
    public void SetProp_WithNil_RemovesIt()
    {
        var result = Run("mobile.set_prop(2, 'quest.step', 2) return mobile.set_prop(2, 'quest.step'), mobile.get_prop(2, 'quest.step')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal(LuaValue.Nil, result[1]);
    }

    [Theory,
     InlineData("return mobile.set_prop(999, 'a', 1)"),
     InlineData("return mobile.set_prop(2, ' ', 1)"),
     InlineData("return mobile.set_prop(2, 'a', {})")]
    public void SetProp_WhatCannotBeKept_IsFalse(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());
    }

    [Fact]
    public void Teleport_AMobileInTheWorld_AsksForTheTeleport()
    {
        var result = Run("return mobile.teleport(2, 5690, 569, 25)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, MapType.Felucca, new Point3D(5690, 569, 25)), Assert.Single(_teleports.Teleports));
    }

    [Fact]
    public void Teleport_WithAMap_AsksForTheTeleportToThatMap()
    {
        var result = Run("return mobile.teleport(2, 100, 200, 5, 4)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, MapType.Tokuno, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Theory,
     InlineData("return mobile.teleport(2, 100, 200, 5, 'Tokuno')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 'tokuno')")]
    public void Teleport_WithAMapName_AsksForTheTeleportToThatMap(string chunk)
    {
        Assert.True(Run(chunk)[0].Read<bool>());

        Assert.Equal((_aria, MapType.Tokuno, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Fact]
    public void Teleport_WithANilMap_StaysOnTheMobilesMap()
    {
        Assert.True(Run("return mobile.teleport(2, 100, 200, 5, nil)")[0].Read<bool>());

        Assert.Equal((_aria, MapType.Felucca, new Point3D(100, 200, 5)), Assert.Single(_teleports.Teleports));
    }

    [Theory,
     InlineData("return mobile.teleport(2, 100, 200, 5, 6)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, -1)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 1.5)"),
     InlineData("return mobile.teleport(2, 100, 200, 5, 'Atlantis')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, '4')"),
     InlineData("return mobile.teleport(2, 100, 200, 5, true)")]
    public void Teleport_ToAMapThatDoesNotExist_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Teleport_Refused_IsFalse()
    {
        _teleports.Result = false;

        Assert.False(Run("return mobile.teleport(2, 5690, 569, 25)")[0].Read<bool>());
    }

    [Theory,
     InlineData("return mobile.teleport(999, 5690, 569, 25)"),
     InlineData("return mobile.teleport(-1, 5690, 569, 25)"),
     InlineData("return mobile.teleport(2, 5690, 569, 128)"),
     InlineData("return mobile.teleport(2, 5690, 569, -129)")]
    public void Teleport_AnUnknownMobileOrAHeightOutOfRange_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Location_GivesWhereTheMobileStands()
    {
        var result = Run("local at = mobile.location(2) return at.x, at.y, at.z, at.map");

        Assert.Equal([1601, 1600, 5, (int)MapType.Felucca], result.Select(value => value.Read<int>()));
    }

    [Fact]
    public void Location_OfAnUnknownMobile_IsNil()
    {
        Assert.Equal(LuaValueType.Nil, Run("return mobile.location(999)")[0].Type);
    }

    [Fact]
    public void PlaySound_PlaysItWhereTheMobileStands()
    {
        var result = Run("return mobile.play_sound(2, 0x1FE)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Theory,
     InlineData("return mobile.play_sound(999, 0x1FE)"),
     InlineData("return mobile.play_sound(2, -1)"),
     InlineData("return mobile.play_sound(2, 65536)")]
    public void PlaySound_AnUnknownMobileOrSound_IsFalseAndSilent(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void Message_TellsThePlayer()
    {
        var result = Run("return mobile.message(2, 'That is too far away.')");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, "That is too far away."), Assert.Single(_speech.Told));
    }

    [Fact]
    public void Message_ALongText_IsCut()
    {
        Run("return mobile.message(2, string.rep('a', 300))");

        Assert.Equal(128, Assert.Single(_speech.Told).Text.Length);
    }

    [Theory, InlineData("return mobile.message(999, 'hello')"), InlineData("return mobile.message(2, '  ')")]
    public void Message_AnUnknownMobileOrAnEmptyText_IsFalseAndTellsNobody(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_speech.Told);
    }

    [Fact]
    public void Template_IsTheIdOfTheMobilesTemplate_AndNilForAPlayerOrAnUnknownMobile()
    {
        var result = Run("return mobile.template(0x100), mobile.template(" + _aria.Id.Value + "), mobile.template(0x999)");

        Assert.Equal("orc", result[0].Read<string>());
        Assert.Equal([LuaValueType.Nil, LuaValueType.Nil], result[1..].Select(value => value.Type));
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new MobileModule(_mobiles, _teleports, _speech, _items, _music, _regions, _light));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
