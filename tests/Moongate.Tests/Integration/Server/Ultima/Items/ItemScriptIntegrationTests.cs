using Lua;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Effects;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

public sealed class ItemScriptIntegrationTests : IAsyncLifetime
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly List<LuaScriptEngineService> _engines = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingEffectService _effects = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubClockService _clock = new();
    private readonly ItemService _items;
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _potions = new() { Id = new Serial(0x40000002), TemplateId = "potion", ItemId = 0x0F0E, Amount = 3 };

    private readonly SettableClock _time = new();

    private BroadcastFixture _fixture = null!;
    private ItemTimerService _itemTimers = null!;

    public ItemScriptIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        _backpack.Equip(new Serial(2), LayerType.Backpack);
        _potions.PutInContainer(_backpack.Id, new Point2D(44, 65));
        _items.Add([_backpack, _potions]);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_sectors);
        _container.RegisterInstance<IClockService>(_clock);
        _container.RegisterInstance<IRegionService>(new RegionService(new StubDataLoaderService().With<RegionContent>()));
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _itemTimers = new(_timers, new ItemTimerQueue(_time), _items, new RecordingItemScriptService(), _time);
        _container.RegisterInstance<IItemTimerService>(_itemTimers);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<WorldModule>();
        _container.RegisterInstance<ITeleportService>(new TeleportService(_fixture.Mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors, new StubBankService()));
        _container.AddScriptModule<MobileModule>();
        _container.RegisterInstance<IEffectService>(_effects);
        _container.AddScriptModule<EffectModule>();
        _container.RegisterScriptEnum<EffectGraphicType>();
        _container.RegisterInstance(TestLocalization.With((398, "C'è una serratura."), (405, "Using your key, you open the door.")));
        _container.AddScriptModule<LocalizationModule>();
        _container.Resolve<IMoongateEventBus>()
            .Subscribe<ScriptErrorEvent>((evt, _) =>
                {
                    _errors.Add(evt);

                    return Task.CompletedTask;
                }
            );
    }

    [Fact]
    public async Task TheShippedPotionScript_DrinksOnePotionAndTellsThePlayer()
    {
        _scripts.Write("items/potion.lua", File.ReadAllText(ShippedScript("items/potion.lua")));
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "potion", ScriptId = "potion" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        var result = scripts.Run(_potions, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (result.Kind, result.Values[0]));
        Assert.Equal(2, _potions.Amount);
        var label = Assert.Single(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>());
        Assert.Equal((SpeechType.Label, "You drink the potion."), (label.Type, label.Text));
    }

    [Fact]
    public async Task AScriptThatReturnsFalse_RefusesTheMove_AnythingElseLetsItFollow()
    {
        _scripts.Write(
            "items/potion.lua",
            """
            potion = {}

            function potion.can_pick_up(serial, picker)
                return picker ~= 2
            end

            function potion.can_drop(serial, dropper)
            end

            function potion.can_equip(serial, wearer)
                error("broken")
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "potion", ScriptId = "potion" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        Assert.False(scripts.Allows(_potions, "can_pick_up", 2L));
        Assert.True(scripts.Allows(_potions, "can_pick_up", 3L));
        Assert.True(scripts.Allows(_potions, "can_drop", 2L));
        Assert.True(scripts.Allows(_potions, "can_insert", 2L, 3L));
        Assert.Empty(_errors);
        // A broken handler does not lock the item: the error is reported and the move follows.
        Assert.True(scripts.Allows(_potions, "can_equip", 2L));
        Assert.Single(_errors);
    }

    [Fact]
    public async Task ADoorLikeScript_SwapsItsGraphicMovesSoundsAndClosesWhenTheDoorwayIsFree()
    {
        var door = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "door", ItemId = 0x0675, Amount = 1 };
        door.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([door]);
        _scripts.Write(
            "items/door.lua",
            """
            door = {}

            function door.on_use(serial, user)
                local here = item.location(serial)

                if item.get_prop(serial, "door.open") then
                    if world.is_occupied(here.map, here.x - 1, here.y + 1) then
                        return true
                    end

                    item.set_item_id(serial, item.item_id(serial) - 1)
                    item.move_to(serial, here.x - 1, here.y + 1, here.z)
                    item.set_prop(serial, "door.open", nil)
                    item.play_sound(serial, 0xF3)
                else
                    item.set_item_id(serial, item.item_id(serial) + 1)
                    item.move_to(serial, here.x + 1, here.y - 1, here.z)
                    item.set_prop(serial, "door.open", true)
                    item.play_sound(serial, 0xEC)
                end

                return true
            end
            """
        );
        using var engine = NewEngine();
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "door", ScriptId = "door" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        scripts.Run(door, "on_use", 2L);
        Assert.Equal((0x0676, new Point3D(1601, 1599, 0)), (door.ItemId, door.GroundLocation!.Value));

        _sectors.Add(new MobileEntity { Id = new Serial(0x100), Name = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        scripts.Run(door, "on_use", 2L);
        Assert.Equal(0x0676, door.ItemId);

        _sectors.Remove(new MobileEntity { Id = new Serial(0x100), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        scripts.Run(door, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x0675, new Point3D(1600, 1600, 0)), (door.ItemId, door.GroundLocation!.Value));
        Assert.Equal([0xEC, 0xF3], _speech.PlacedSounds.Select(sound => sound.Sound));
    }

    [Fact]
    public async Task TheShippedDoorScript_OpensTheDoorAndItsLinkedDoor_WithTheirSounds()
    {
        var (left, right) = PlaceDoubleDoor();
        var scripts = await StartDoorScriptAsync();

        var result = scripts.Run(left, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (result.Kind, result.Values[0]));
        Assert.Equal((0x0676, new Point3D(1599, 1601, 0)), (left.ItemId, left.GroundLocation!.Value));
        Assert.Equal((0x0678, new Point3D(1602, 1601, 0)), (right.ItemId, right.GroundLocation!.Value));
        Assert.Equal([0xEC, 0xEC], _speech.PlacedSounds.Select(sound => sound.Sound));
        Assert.Equal(
            [true, 1600L, 1600L, 0L],
            new[] { left.Props!["door.open"], left.Props["door.x"], left.Props["door.y"], left.Props["door.z"] }
        );
    }

    [Fact]
    public async Task TheShippedDoorScript_ADoorThatCannotSwingAside_StaysClosed()
    {
        var door = PlaceDoor(new Serial(0x40000010), "MetalDoor", 0x0675, "west_cw", new Point3D(0, 1600, 0));
        var scripts = await StartDoorScriptAsync();

        scripts.Run(door, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x0675, new Point3D(0, 1600, 0)), (door.ItemId, door.GroundLocation!.Value));
        Assert.False(door.Props!.ContainsKey("door.open"));
        Assert.Empty(_speech.PlacedSounds);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task TheShippedDoorScript_ALockedDoor_StaysClosedForAPlayer_AndSaysSo()
    {
        var (left, right) = PlaceDoubleDoor();
        left.Props!["locked"] = true;
        var scripts = await StartDoorScriptAsync();

        var result = scripts.Run(left, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (result.Kind, result.Values[0]));
        Assert.Equal((0x0675, 0x0677), (left.ItemId, right.ItemId));
        var label = Assert.Single(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>());
        Assert.Equal((SpeechType.Label, "C'è una serratura."), (label.Type, label.Text));
        Assert.Empty(_speech.PlacedSounds);
    }

    [Fact]
    public async Task TheShippedDoorScript_ALockedDoor_OpensForAPlayerCarryingItsKey_AndStaysLocked()
    {
        var (left, right) = PlaceDoubleDoor();
        left.Props!["locked"] = true;
        left.Props["key.value"] = 77L;
        var key = new ItemEntity
        {
            Id = new Serial(0x40000030), TemplateId = "0x1010_iron_key", ItemId = 0x1010, Amount = 1,
            Props = new() { ["key.value"] = 77L }
        };
        key.PutInContainer(_backpack.Id, new Point2D(50, 70));
        _items.Add([key]);
        var scripts = await StartDoorScriptAsync();

        scripts.Run(left, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x0676, 0x0678, (object?)true), (left.ItemId, right.ItemId, left.Props["locked"]));
        Assert.Equal("Using your key, you open the door.", Assert.Single(_fixture.Sender.Sent.OfType<UnicodeSpeechMessagePacket>()).Text);
    }

    [Fact]
    public async Task TheShippedDoorScript_ALockedDoor_StaysShutForAnotherKey()
    {
        var (left, _) = PlaceDoubleDoor();
        left.Props!["locked"] = true;
        left.Props["key.value"] = 77L;
        var key = new ItemEntity
        {
            Id = new Serial(0x40000030), TemplateId = "0x1010_iron_key", ItemId = 0x1010, Amount = 1,
            Props = new() { ["key.value"] = 78L }
        };
        key.PutInContainer(_backpack.Id, new Point2D(50, 70));
        _items.Add([key]);
        var scripts = await StartDoorScriptAsync();

        scripts.Run(left, "on_use", 2L);

        Assert.Equal(0x0675, left.ItemId);
    }

    [Fact]
    public async Task TheShippedDoorScript_ALockedDoor_OpensForStaff()
    {
        var (left, right) = PlaceDoubleDoor();
        left.Props!["locked"] = true;
        var scripts = await StartDoorScriptAsync();
        _fixture.Sessions.TryGetByCharacterId(new Serial(2), out var session);
        await _fixture.Network.ExecuteOnLoopAsync(() => session!.Set(SessionKeys.AccountType, AccountType.GameMaster));

        scripts.Run(left, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x0676, 0x0678), (left.ItemId, right.ItemId));
    }

    [Fact]
    public async Task TheShippedDoorScript_ClosesBothDoorsOnlyWhenBothDoorwaysAreFree()
    {
        var (left, right) = PlaceDoubleDoor();
        var scripts = await StartDoorScriptAsync();
        scripts.Run(left, "on_use", 2L);
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "orc", Map = MapType.Trammel, Location = new Point3D(1601, 1600, 0) };

        _sectors.Add(orc);
        scripts.Run(right, "on_use", 2L);
        Assert.Equal((0x0676, 0x0678), (left.ItemId, right.ItemId));

        _sectors.Remove(orc);
        scripts.Run(right, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x0675, new Point3D(1600, 1600, 0)), (left.ItemId, left.GroundLocation!.Value));
        Assert.Equal((0x0677, new Point3D(1601, 1600, 0)), (right.ItemId, right.GroundLocation!.Value));
        Assert.Equal([0xEC, 0xEC, 0xF3, 0xF3], _speech.PlacedSounds.Select(sound => sound.Sound));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task TheShippedDoorScript_ClosesByItselfAfter20Seconds_RetryingEvery10WhileTheDoorwayIsTaken()
    {
        var door = PlaceDoor(new Serial(0x40000010), "DarkWoodGate", 0x0839, "south_cw", new Point3D(1600, 1600, 0));
        var scripts = await StartDoorScriptAsync();
        scripts.Run(door, "on_use", 2L);
        // The timer is a prop of the door, so the world save keeps it and a restart does not lose it.
        Assert.Equal(TimeSpan.FromSeconds(20), _itemTimers.Remaining(door, "close"));
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) };
        _sectors.Add(orc);

        // What the timer service does when the time has come.
        _itemTimers.Stop(door, "close");
        scripts.Run(door, "on_timer", "close");
        Assert.Equal((TimeSpan.FromSeconds(10), 0x083A), (_itemTimers.Remaining(door, "close"), door.ItemId));

        _sectors.Remove(orc);
        _itemTimers.Stop(door, "close");
        scripts.Run(door, "on_timer", "close");

        Assert.Empty(_errors);
        Assert.Equal((0x0839, new Point3D(1600, 1600, 0)), (door.ItemId, door.GroundLocation!.Value));
        Assert.Equal([0xEB, 0xF2], _speech.PlacedSounds.Select(sound => sound.Sound));
        Assert.Null(_itemTimers.Remaining(door, "close"));
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task TheShippedDoorScript_ASecretDoorWithoutFacing_OpensInPlace()
    {
        var door = PlaceDoor(new Serial(0x40000010), "SecretStoneDoor1", 0x00E8, null, new Point3D(1600, 1600, 0));
        var scripts = await StartDoorScriptAsync();

        scripts.Run(door, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((0x00E9, new Point3D(1600, 1600, 0)), (door.ItemId, door.GroundLocation!.Value));
        Assert.Equal([0xED], _speech.PlacedSounds.Select(sound => sound.Sound));
    }

    [Fact]
    public async Task TheShippedTeleporterScript_MovesWhoeverStepsOnIt_AndSoundsAtTheDestination()
    {
        var teleporter = PlaceTeleporter(
            new() { ["teleport.x"] = 5690L, ["teleport.y"] = 569L, ["teleport.z"] = 25L, ["sound_id"] = 0x1FEL }
        );
        var scripts = await StartTeleporterScriptAsync();
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));

        scripts.Run(teleporter, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(5690, 569, 25), aria.Location);
        Assert.Single(_fixture.Sender.Sent.OfType<MobileUpdatePacket>());
        Assert.Equal((aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Theory]
    // Turned off.
    [InlineData(false, 5690L, null)]
    // No destination.
    [InlineData(null, null, null)]
    // To a map that is not loaded.
    [InlineData(null, 5690L, 4L)]
    public async Task TheShippedTeleporterScript_OffWithoutADestinationOrToAMapNotLoaded_DoesNothing(bool? active, long? x, long? map)
    {
        var props = new Dictionary<string, object?> { ["teleport.y"] = 569L, ["teleport.z"] = 25L, ["sound_id"] = 0x1FEL };

        if (active is not null)
        {
            props["active"] = active;
        }

        if (x is not null)
        {
            props["teleport.x"] = x;
        }

        if (map is not null)
        {
            props["teleport.map"] = map;
        }

        var teleporter = PlaceTeleporter(props);
        var scripts = await StartTeleporterScriptAsync();
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        var start = aria.Location;

        scripts.Run(teleporter, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Equal(start, aria.Location);
        Assert.Empty(_speech.Sounds);
    }

    [Theory,
     InlineData("true", "true", true, true),
     InlineData("true", null, true, false),
     InlineData(null, "true", false, true),
     InlineData(null, null, false, false)]
    public async Task TheShippedTeleporterScript_ShowsTheSmokeWhereItsPropsAskForIt(
        string? sourceEffect,
        string? destEffect,
        bool atSource,
        bool atDestination
    )
    {
        // The decoration files carry these flags as text; as ModernUO, the smoke stays where the mobile left and arrived.
        var teleporter = PlaceTeleporter(
            new()
            {
                ["teleport.x"] = 5690L, ["teleport.y"] = 569L, ["teleport.z"] = 25L, ["source_effect"] = sourceEffect,
                ["dest_effect"] = destEffect
            }
        );
        var scripts = await StartTeleporterScriptAsync();
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        var source = aria.Location;
        var expected = new List<Point3D>();

        if (atSource)
        {
            expected.Add(source);
        }

        if (atDestination)
        {
            expected.Add(new Point3D(5690, 569, 25));
        }

        scripts.Run(teleporter, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(5690, 569, 25), aria.Location);
        Assert.Equal(expected, _effects.At.Select(effect => effect.Location));
        Assert.All(_effects.At, effect => Assert.Equal((aria.Map, (int)EffectGraphicType.Smoke), (effect.Map, effect.Options.Graphic)));
    }

    [Fact]
    public async Task TheShippedKeywordTeleporterScript_TheWordSaidOnItsCell_TeleportsWithSmokeAndSound()
    {
        var teleporter = PlaceKeywordTeleporter(Mantra());
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(1600, 1600);

        scripts.Run(teleporter, "on_speech", 2L, "I say Om Om Om here", new LuaTable());

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1595, 2489, 20), aria.Location);
        Assert.Equal([new Point3D(1600, 1600, 0), new Point3D(1595, 2489, 20)], _effects.At.Select(effect => effect.Location));
        Assert.Equal((aria, 0x1FE), Assert.Single(_speech.Sounds));
        Assert.Empty(_timers.Timers);
    }

    [Theory,
     InlineData("hello there", 1600, 1600, null),
     InlineData("om om om", 1601, 1600, null),
     InlineData("om om om", 1600, 1600, false)]
    public async Task TheShippedKeywordTeleporterScript_AnotherWordOutOfRangeOrOff_DoesNothing(
        string text,
        int x,
        int y,
        bool? active
    )
    {
        var props = Mantra();
        props["active"] = active;
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(x, y);

        scripts.Run(teleporter, "on_speech", 2L, text, new LuaTable());

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(x, y, 0), aria.Location);
        Assert.Empty(_effects.At);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public async Task TheShippedKeywordTeleporterScript_ToAnotherMap_ChangesTheMap_WithTheSmokeThere()
    {
        var props = Mantra();
        props["teleport.map"] = (long)MapType.Felucca;
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(1600, 1600);

        scripts.Run(teleporter, "on_speech", 2L, "om om om", new LuaTable());

        Assert.Empty(_errors);
        Assert.Equal((MapType.Felucca, new Point3D(1595, 2489, 20)), (aria.Map, aria.Location));
        Assert.Equal([MapType.Trammel, MapType.Felucca], _effects.At.Select(effect => effect.Map));
    }

    [Fact]
    public async Task TheShippedKeywordTeleporterScript_WithinItsRange_AnswersTheSpeechKeywordToo()
    {
        var props = Mantra();
        props["range"] = 2L;
        props["keyword"] = 0x3BL;
        props["substring"] = null;
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(1602, 1598);
        var keywords = new LuaTable();
        keywords[1] = 0x3B;

        scripts.Run(teleporter, "on_speech", 2L, "anything", keywords);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1595, 2489, 20), aria.Location);
    }

    [Theory,
     InlineData("0:0:2", 2.0),
     InlineData("00:01:01.5", 61.5),
     InlineData("3", 3.0),
     InlineData(1.5, 1.5)]
    public async Task TheShippedKeywordTeleporterScript_ReadsADelayAsATimeOrAsSeconds(object delay, double seconds)
    {
        var props = Mantra();
        props["delay"] = delay;
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        AriaAt(1600, 1600);

        scripts.Run(teleporter, "on_speech", 2L, "om om om", new LuaTable());

        Assert.Empty(_errors);
        Assert.Equal(TimeSpan.FromSeconds(seconds), Assert.Single(_timers.Timers).Interval);
    }

    [Fact]
    public async Task TheShippedKeywordTeleporterScript_ARangeWrittenAsText_IsStillARange()
    {
        // A prop edited by hand: without the conversion every line spoken nearby raised a Lua error.
        var props = Mantra();
        props["range"] = "2";
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(1602, 1600);

        scripts.Run(teleporter, "on_speech", 2L, "om om om", new LuaTable());

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1595, 2489, 20), aria.Location);
    }

    [Fact]
    public async Task TheShippedKeywordTeleporterScript_WithADelay_TeleportsWhenItEndsIfThePlayerIsStillThere()
    {
        var props = Mantra();
        props["delay"] = "0:0:1";
        var teleporter = PlaceKeywordTeleporter(props);
        var scripts = await StartKeywordTeleporterScriptAsync();
        var aria = AriaAt(1600, 1600);

        scripts.Run(teleporter, "on_speech", 2L, "om om om", new LuaTable());
        var first = Assert.Single(_timers.Timers);
        Assert.Equal((TimeSpan.FromSeconds(1), new Point3D(1600, 1600, 0)), (first.Interval, aria.Location));

        // The player walked away before the second ended: as ModernUO, nothing happens.
        aria.Location = new Point3D(1610, 1600, 0);
        _timers.Fire(first.Id);
        Assert.Equal(new Point3D(1610, 1600, 0), aria.Location);

        aria.Location = new Point3D(1600, 1600, 0);
        scripts.Run(teleporter, "on_speech", 2L, "om om om", new LuaTable());
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(1595, 2489, 20), aria.Location);
    }

    [Fact]
    public async Task TheShippedTeleporterScript_ToAnotherMap_ChangesTheMap_AndSoundsThere()
    {
        var teleporter = PlaceTeleporter(
            new()
            {
                ["teleport.x"] = 5690L, ["teleport.y"] = 569L, ["teleport.z"] = 25L, ["teleport.map"] = (long)MapType.Felucca,
                ["sound_id"] = 0x1FEL
            }
        );
        var scripts = await StartTeleporterScriptAsync();
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));

        scripts.Run(teleporter, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Equal((MapType.Felucca, new Point3D(5690, 569, 25)), (aria.Map, aria.Location));
        Assert.Equal(MapType.Felucca, Assert.Single(_fixture.Sender.Sent.OfType<MapChangePacket>()).Map);
        Assert.Equal((aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(true, true)]
    [InlineData("true", true)]
    public async Task TheShippedTeleporterScript_AnNpc_TravelsOnlyThroughATeleporterForCreatures(object? creatures, bool travels)
    {
        var props = new Dictionary<string, object?> { ["teleport.x"] = 5690L, ["teleport.y"] = 569L, ["teleport.z"] = 25L };

        if (creatures is not null)
        {
            props["creatures"] = creatures;
        }

        var teleporter = PlaceTeleporter(props);
        var scripts = await StartTeleporterScriptAsync();
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) };
        _fixture.Mobiles.EnterWorld(orc);

        scripts.Run(teleporter, "on_npc_move_over", 0x100L);

        Assert.Empty(_errors);
        Assert.Equal(travels ? new Point3D(5690, 569, 25) : new Point3D(1600, 1600, 0), orc.Location);
    }

    [Fact]
    public async Task TheShippedTeleporterScript_ToItsOwnMap_Teleports()
    {
        var teleporter = PlaceTeleporter(
            new() { ["teleport.x"] = 5690L, ["teleport.y"] = 569L, ["teleport.z"] = 25L, ["teleport.map"] = (long)MapType.Trammel }
        );
        var scripts = await StartTeleporterScriptAsync();
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));

        scripts.Run(teleporter, "on_move_over", 2L);

        Assert.Empty(_errors);
        Assert.Equal(new Point3D(5690, 569, 25), aria.Location);
    }

    [Fact]
    public async Task TheShippedLightScript_LightsACandleWithItsShape_AndDousesIt()
    {
        var candle = PlaceLight(0x0A28, null);
        var scripts = await StartLightScriptAsync();

        var result = scripts.Run(candle, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (result.Kind, result.Values[0]));
        Assert.Equal((0x0A0F, (object?)"circle150"), (candle.ItemId, candle.Props?.GetValueOrDefault("light")));

        scripts.Run(candle, "on_use", 2L);

        Assert.Equal(0x0A28, candle.ItemId);
        Assert.Equal([0x47, 0x3BE], _speech.PlacedSounds.Select(sound => sound.Sound));
    }

    [Fact]
    public async Task TheShippedLightScript_KeepsTheShapeALightAlreadyHas()
    {
        var sconce = PlaceLight(0x0A00, null);
        sconce.Props = new() { ["light"] = "north_big" };
        var scripts = await StartLightScriptAsync();

        scripts.Run(sconce, "on_use", 2L);

        Assert.Equal((0x0A02, (object?)"north_big"), (sconce.ItemId, sconce.Props!["light"]));
    }

    [Fact]
    public async Task TheShippedLightScript_AProtectedLight_OnlyStaffLightsIt()
    {
        var lamp = PlaceLight(0x0B21, true);
        var scripts = await StartLightScriptAsync();

        scripts.Run(lamp, "on_use", 2L);
        Assert.Equal(0x0B21, lamp.ItemId);

        _fixture.Sessions.TryGetByCharacterId(new Serial(2), out var session);
        await _fixture.Network.ExecuteOnLoopAsync(() => session!.Set(SessionKeys.AccountType, AccountType.GameMaster));
        scripts.Run(lamp, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal(0x0B20, lamp.ItemId);
    }

    [Fact]
    public async Task TheShippedLightScript_ALightWithoutAnUnlitGraphic_StaysAsItIs()
    {
        var brazier = PlaceLight(0x0E31, null);
        var scripts = await StartLightScriptAsync();

        var result = scripts.Run(brazier, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal(((object?)true, 0x0E31), (result.Values[0], brazier.ItemId));
        Assert.Empty(_speech.PlacedSounds);
    }

    [Fact]
    public async Task TheShippedLightScript_AHeatingStand_LightsWithTheSmallCircle()
    {
        var stand = PlaceLight(0x1849, null);
        var scripts = await StartLightScriptAsync();

        scripts.Run(stand, "on_use", 2L);

        Assert.Equal((0x184A, (object?)"circle150"), (stand.ItemId, stand.Props!["light"]));
    }

    [Fact]
    public async Task TheShippedLightScript_ALampPost_LightsInTheDark_AndDousesInTheLight_Silently()
    {
        var lamp = PlaceLight(0x0B21, true);
        var scripts = await StartLightScriptAsync();

        scripts.Run(lamp, "on_darkness", true);
        Assert.Equal((0x0B20, (object?)"circle300"), (lamp.ItemId, lamp.Props!["light"]));

        scripts.Run(lamp, "on_darkness", true);
        Assert.Equal(0x0B20, lamp.ItemId);

        scripts.Run(lamp, "on_darkness", false);

        Assert.Empty(_errors);
        Assert.Equal(0x0B21, lamp.ItemId);
        Assert.Empty(_speech.PlacedSounds);
    }

    private ItemEntity PlaceLight(int graphic, bool? isProtected)
    {
        var light = new ItemEntity { Id = new Serial(0x40000020), TemplateId = "decoration_light", ItemId = graphic, Amount = 1 };

        if (isProtected is { } value)
        {
            light.Props = new() { ["protected"] = value };
        }

        light.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([light]);

        return light;
    }

    // As ModernUO's Clock: the part of the day, then the time to the minute, as texts of the client over the clock.
    [Theory]
    [InlineData(0, 30, 1042950, "12:30")]
    [InlineData(1, 0, 1042951, "1:00")]
    [InlineData(3, 59, 1042951, "3:59")]
    [InlineData(4, 5, 1042952, "4:05")]
    [InlineData(8, 0, 1042953, "8:00")]
    [InlineData(12, 0, 1042954, "12:00")]
    [InlineData(13, 7, 1042955, "1:07")]
    [InlineData(16, 0, 1042956, "4:00")]
    [InlineData(20, 0, 1042957, "8:00")]
    [InlineData(23, 59, 1042957, "11:59")]
    public async Task TheShippedClockScript_TellsThePartOfTheDayAndTheTimeToTheMinute(int hours, int minutes, int part, string exact)
    {
        _clock.Time = new(hours, minutes);
        var scripts = await StartItemScriptAsync("clock", "clock");
        var clock = new ItemEntity { Id = new Serial(0x40000080), TemplateId = "clock", ItemId = 0x104B, Amount = 1 };
        clock.PutInContainer(_backpack.Id, new Point2D(50, 50), 1);
        _items.Add([clock]);

        var result = scripts.Run(clock, "on_use", 2L);

        Assert.Empty(_errors);
        Assert.Equal((ScriptResultKind.Completed, true), (result.Kind, result.Values[0]));
        var labels = _fixture.Sender.Sent.OfType<LocalizedMessagePacket>().ToList();
        Assert.Equal([(part, ""), (1042958, exact)], labels.Select(label => (label.Cliloc, label.Arguments)));
        Assert.All(labels, label => Assert.Equal(clock.Id, label.Serial));
    }

    private async Task<ItemScriptService> StartLightScriptAsync()
    {
        _scripts.Write("items/light.lua", File.ReadAllText(ShippedScript("items/light.lua")));
        var engine = NewEngine();
        _engines.Add(engine);
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "decoration_light", ScriptId = "light" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        return scripts;
    }

    private ItemEntity PlaceTeleporter(Dictionary<string, object?> props)
    {
        var teleporter = new ItemEntity
        {
            Id = new Serial(0x40000030), TemplateId = "decoration_teleporter", ItemId = 0x1BC3, Amount = 1, Props = props
        };
        teleporter.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([teleporter]);

        return teleporter;
    }

    private static Dictionary<string, object?> Mantra()
    {
        return new()
        {
            ["substring"] = "om om om", ["range"] = 0L, ["teleport.x"] = 1595L, ["teleport.y"] = 2489L,
            ["teleport.z"] = 20L, ["source_effect"] = "true", ["dest_effect"] = "true", ["sound_id"] = 0x1FEL
        };
    }

    private MobileEntity AriaAt(int x, int y)
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        aria.Map = MapType.Trammel;
        aria.Location = new Point3D(x, y, 0);

        return aria;
    }

    private ItemEntity PlaceKeywordTeleporter(Dictionary<string, object?> props)
    {
        var teleporter = new ItemEntity
        {
            Id = new Serial(0x40000031), TemplateId = "decoration_keyword_teleporter", ItemId = 0x1BC3, Amount = 1,
            Props = props
        };
        teleporter.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 12));
        _items.Add([teleporter]);

        return teleporter;
    }

    private Task<ItemScriptService> StartKeywordTeleporterScriptAsync()
    {
        return StartItemScriptAsync("keyword_teleport", "decoration_keyword_teleporter");
    }

    private Task<ItemScriptService> StartTeleporterScriptAsync()
    {
        return StartItemScriptAsync("teleporter", "decoration_teleporter");
    }

    private async Task<ItemScriptService> StartItemScriptAsync(string script, string template)
    {
        _scripts.Write($"items/{script}.lua", File.ReadAllText(ShippedScript($"items/{script}.lua")));
        var engine = NewEngine();
        _engines.Add(engine);
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(
                new StubDataLoaderService().With(new ItemTemplate { Id = template, ScriptId = script })
            ),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        return scripts;
    }

    private (ItemEntity Left, ItemEntity Right) PlaceDoubleDoor()
    {
        var left = PlaceDoor(new Serial(0x40000010), "MetalDoor", 0x0675, "west_cw", new Point3D(1600, 1600, 0));
        var right = PlaceDoor(new Serial(0x40000011), "MetalDoor", 0x0677, "east_ccw", new Point3D(1601, 1600, 0));
        left.Props!["door.link"] = (long)right.Id.Value;
        right.Props!["door.link"] = (long)left.Id.Value;

        return (left, right);
    }

    private ItemEntity PlaceDoor(Serial serial, string type, int graphic, string? facing, Point3D location)
    {
        var door = new ItemEntity
        {
            Id = serial, TemplateId = "decoration_door", ItemId = graphic, Amount = 1,
            Props = new() { ["decoration_type"] = type }
        };

        if (facing is not null)
        {
            door.Props["facing"] = facing;
        }

        door.PlaceOnGround(MapType.Trammel, location);
        _items.Add([door]);

        return door;
    }

    private async Task<ItemScriptService> StartDoorScriptAsync()
    {
        _scripts.Write("items/door.lua", File.ReadAllText(ShippedScript("items/door.lua")));
        var engine = NewEngine();
        _engines.Add(engine);
        await engine.StartAsync();
        var scripts = new ItemScriptService(
            engine,
            new ItemTemplateService(new StubDataLoaderService().With(new ItemTemplate { Id = "decoration_door", ScriptId = "door" })),
            _loop,
            new ScriptEngineOptions { ScriptsDirectory = _scripts.Path }
        );
        await scripts.StartAsync();

        return scripts;
    }

    public async Task DisposeAsync()
    {
        foreach (var engine in _engines)
        {
            engine.Dispose();
        }

        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    private LuaScriptEngineService NewEngine()
    {
        return new(
            new ScriptEngineOptions
            {
                ScriptsDirectory = _scripts.Path,
                MaxInstructionsPerResume = 20_000,
                MaxInstructionsPerChunk = 100_000,
                HookInterval = 100
            },
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
