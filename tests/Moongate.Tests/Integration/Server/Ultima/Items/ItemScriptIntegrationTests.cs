using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Tests.TestSupport.Scripting;
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
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly ItemService _items;
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _potions = new() { Id = new Serial(0x40000002), TemplateId = "potion", ItemId = 0x0F0E, Amount = 3 };

    private BroadcastFixture _fixture = null!;

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
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<WorldModule>();
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

    public async Task DisposeAsync()
    {
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
