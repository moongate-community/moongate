using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.HuePicking;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Items;

/// <summary>
///     The shipped <c>scripts/items/dyes.lua</c> and <c>scripts/items/dye_tub.lua</c>, with the real Lua engine.
/// </summary>
public sealed class DyeScriptsIntegrationTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Bruno = 3;

    private const int SelectTub = 500856;
    private const int UseOnATub = 500857;
    private const int SelectClothing = 500859;
    private const int TooFar = 500446;
    private const int Worn = 500861;
    private const int CannotDye = 1042083;
    private const int DyeSound = 0x23E;

    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorEvent> _errors = [];
    private readonly SectorService _sectors = TestSectors.Create();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubTargetService _targets = new();
    private readonly StubHuePickerService _pickers = new();
    private readonly ItemService _items;
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "dyes", ItemId = new Serial(0x0FA9), ScriptId = "dyes" },
            new ItemTemplate { Id = "tub", ItemId = new Serial(0x0FAB), ScriptId = "dye_tub" },
            new ItemTemplate { Id = "shirt", ItemId = new Serial(0x1517), Layer = LayerType.Shirt, Dyeable = true },
            new ItemTemplate { Id = "sword", ItemId = new Serial(0x0F5E) }
        )
    );
    private readonly ItemEntity _backpack = new() { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    private readonly ItemEntity _dyes = new() { Id = new Serial(0x40000002), TemplateId = "dyes", ItemId = 0x0FA9, Amount = 1 };
    private readonly ItemEntity _tub = new() { Id = new Serial(0x40000003), TemplateId = "tub", ItemId = 0x0FAB, Amount = 1 };
    private readonly ItemEntity _shirt = new() { Id = new Serial(0x40000004), TemplateId = "shirt", ItemId = 0x1517, Amount = 1 };
    private readonly ItemEntity _sword = new() { Id = new Serial(0x40000005), TemplateId = "sword", ItemId = 0x0F5E, Amount = 1 };
    private readonly ItemEntity _otherBackpack = new() { Id = new Serial(0x40000006), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private LuaScriptEngineService _engine = null!;
    private ItemScriptService _itemScripts = null!;
    private MobileEntity _aria = null!;

    public DyeScriptsIntegrationTests()
    {
        _items = TestItems.Create(_sectors);
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync((int)Aria);
        await _fixture.AddAsync((int)Bruno);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        _backpack.Equip(new Serial((uint)Aria), LayerType.Backpack);
        _otherBackpack.Equip(new Serial((uint)Bruno), LayerType.Backpack);

        foreach (var item in new[] { _dyes, _tub, _shirt, _sword })
        {
            item.PutInContainer(_backpack.Id, new Point2D(44, 65));
        }

        _items.Add([_backpack, _otherBackpack, _dyes, _tub, _shirt, _sword]);
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.RegisterInstance<IItemService>(_items);
        _container.RegisterInstance<IItemTemplateService>(_templates);
        _container.RegisterInstance<ISessionService>(_fixture.Sessions);
        _container.RegisterInstance<IPacketSendService>(_fixture.Sender);
        _container.RegisterInstance<IWorldViewService>(_view);
        _container.RegisterInstance<IMobileService>(_fixture.Mobiles);
        _container.RegisterInstance<ISpeechService>(_speech);
        _container.RegisterInstance<ISectorService>(_sectors);
        _container.RegisterInstance<ITooltipService>(TestTooltips.Create(_items, _fixture.Mobiles));
        _container.RegisterInstance<ITargetService>(_targets);
        _container.RegisterInstance<IHuePickerService>(_pickers);
        _container.RegisterInstance<ITeleportService>(
            new TeleportService(_fixture.Mobiles, _view, _fixture.Sessions, _fixture.Sender, _fixture.Sectors, new StubBankService())
        );
        _container.Register<IItemHandlingService, ItemHandlingService>(Reuse.Singleton);
        _container.AddScriptModule<ItemModule>();
        _container.AddScriptModule<MobileModule>();
        _container.AddScriptModule<TargetModule>();
        _container.AddScriptModule<HuePickerModule>();
        _container.RegisterDelegate<IScriptEngine>(_ => _engine);
        _container.Resolve<IMoongateEventBus>()
                  .Subscribe<ScriptErrorEvent>(
                      (evt, _) =>
                      {
                          _errors.Add(evt);

                          return Task.CompletedTask;
                      }
                  );

        foreach (var script in new[] { "items/dyes.lua", "items/dye_tub.lua", "common/dye.lua" })
        {
            _scripts.Write(script, File.ReadAllText(ShippedScript(script)));
        }

        _engine = new(
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
        await _engine.StartAsync();
        _itemScripts = new(_engine, _templates, _loop, new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
        await _itemScripts.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void Dyes_OnADyeTub_OpenThePickerWithTheTubInIt_AndTheTubTakesTheHuePicked()
    {
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Result = 0x0026;

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal([SelectTub], Told());
        Assert.Equal([0x0FAB], _pickers.Graphics);
        Assert.Equal(0x0026, _tub.Hue.Value);
    }

    [Fact]
    public void Dyes_OnWhatIsNotADyeTub_SayToUseThemOnOne_AndOpenNoPicker()
    {
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal([SelectTub, UseOnATub], Told());
        Assert.Empty(_pickers.Graphics);
    }

    [Fact]
    public void Dyes_OnATubOutOfReach_SayItIsTooFar()
    {
        OnTheGround(_tub, 5);
        _targets.Result = TargetResult.ForObject(_tub.Id);

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal([SelectTub, TooFar], Told());
        Assert.Empty(_pickers.Graphics);
    }

    [Fact]
    public void Dyes_OnATubOnTheGroundBesideThePlayer_Work()
    {
        OnTheGround(_tub, 1);
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Result = 0x0030;

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal(0x0030, _tub.Hue.Value);
    }

    [Fact]
    public void Dyes_WhenTheCursorIsPutAway_DoNothingMore()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal([SelectTub], Told());
        Assert.Empty(_pickers.Graphics);
    }

    [Fact]
    public void Dyes_APickerThatEndsWithoutAnAnswer_LeavesTheTubAsItWas()
    {
        _tub.Hue = new Hue(0x0044);
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Result = null;

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal(0x0044, _tub.Hue.Value);
        Assert.Equal([SelectTub], Told());
    }

    [Fact]
    public void Dyes_ThePickerAnsweredAfterTheTubWentOutOfReach_ChangeNothing()
    {
        // The other emulators take the answer whenever it comes.
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Defer = true;
        Use(_dyes);

        OnTheGround(_tub, 5);
        _pickers.Answer(0x0026);

        Assert.Empty(_errors);
        Assert.Equal(0, _tub.Hue.Value);
        Assert.Equal([SelectTub, TooFar], Told());
    }

    [Fact]
    public void Dyes_ThePickerAnsweredAfterTheDyesAreGone_ChangeNothing()
    {
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Defer = true;
        Use(_dyes);

        _items.Remove([_dyes.Id]);
        _pickers.Answer(0x0026);

        Assert.Empty(_errors);
        Assert.Equal(0, _tub.Hue.Value);
    }

    [Fact]
    public void ADyeTub_OnADyeableItem_GivesItItsHue_WithTheSoundOfDyeing()
    {
        _tub.Hue = new Hue(0x0026);
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing], Told());
        Assert.Equal(0x0026, _shirt.Hue.Value);
        Assert.Contains(DyeSound, _speech.Sounds.Select(sound => sound.Sound).Concat(_speech.PlacedSounds.Select(sound => sound.Sound)));
    }

    [Fact]
    public void ADyeTub_OnADyeableItemOnTheGroundBesideThePlayer_DyesIt()
    {
        _tub.Hue = new Hue(0x0026);
        OnTheGround(_shirt, 1);
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal(0x0026, _shirt.Hue.Value);
    }

    [Fact]
    public void ADyeTub_OnWhatIsNotDyeable_SaysSo()
    {
        _tub.Hue = new Hue(0x0026);
        _targets.Result = TargetResult.ForObject(_sword.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, CannotDye], Told());
        Assert.Equal(0, _sword.Hue.Value);
        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public void ADyeTub_OnAMobile_SaysItCannotBeDyed()
    {
        _targets.Result = TargetResult.ForObject(new Serial((uint)Bruno));

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, CannotDye], Told());
    }

    [Fact]
    public void ADyeTub_OnClothingThatIsWorn_SaysSo_AndLeavesItsHue()
    {
        _tub.Hue = new Hue(0x0026);
        Move(_shirt, () => _shirt.Equip(new Serial((uint)Aria), LayerType.Shirt));
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, Worn], Told());
        Assert.Equal(0, _shirt.Hue.Value);
    }

    [Fact]
    public void ADyeTub_OnADyeableItemOutOfReach_SaysItIsTooFar()
    {
        _tub.Hue = new Hue(0x0026);
        OnTheGround(_shirt, 5);
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, TooFar], Told());
        Assert.Equal(0, _shirt.Hue.Value);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void ADyeTub_OnClothingOfAnotherPlayer_CarriedOrWorn_IsTooFar(bool worn)
    {
        _tub.Hue = new Hue(0x0026);

        Move(
            _shirt,
            () =>
            {
                if (worn)
                {
                    _shirt.Equip(new Serial((uint)Bruno), LayerType.Shirt);
                }
                else
                {
                    _shirt.PutInContainer(_otherBackpack.Id, new Point2D(44, 65));
                }
            }
        );

        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, TooFar], Told());
        Assert.Equal(0, _shirt.Hue.Value);
    }

    [Fact]
    public async Task ADyeTub_OnAnItemHeldOnTheCursor_SaysItCannotBeDyed()
    {
        _tub.Hue = new Hue(0x0026);
        await HoldAsync(_shirt);
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing, CannotDye], Told());
        Assert.Equal(0, _shirt.Hue.Value);
        Assert.Empty(_speech.Sounds);
    }

    [Fact]
    public async Task Dyes_OnATubHeldOnTheCursor_SayItCannotBeDyed()
    {
        await HoldAsync(_tub);
        _targets.Result = TargetResult.ForObject(_tub.Id);
        _pickers.Result = 0x0026;

        Use(_dyes);

        Assert.Empty(_errors);
        Assert.Equal([SelectTub, CannotDye], Told());
        Assert.Equal(0, _tub.Hue.Value);
    }

    [Fact]
    public void ADyeTub_WhenTheCursorIsPutAway_DoesNothingMore()
    {
        _targets.Result = TargetResult.Canceled(TargetCancelType.Canceled);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal([SelectClothing], Told());
    }

    [Fact]
    public void ADyeTubNeverDyed_TakesTheColourOff()
    {
        // As ModernUO: a new tub has hue 0, the colours of the art.
        _shirt.Hue = new Hue(0x0026);
        _targets.Result = TargetResult.ForObject(_shirt.Id);

        Use(_tub);

        Assert.Empty(_errors);
        Assert.Equal(0, _shirt.Hue.Value);
    }

    // The double click, then the turns of the loop on which the cursor and the picker answer.
    private void Use(ItemEntity item)
    {
        _loop.DeferTryPost = true;
        _itemScripts.Run(item, "on_use", Aria);

        while (_loop.Deferred.Count > 0)
        {
            _loop.RunDeferred();
        }

        _loop.DeferTryPost = false;
    }

    // The client texts Aria was told, in order.
    private List<int> Told()
    {
        return _speech.ToldClilocs.Where(told => told.Player.Id == _aria.Id).Select(told => told.Cliloc).ToList();
    }

    // Aria lifts the item: it keeps its place until it is dropped.
    private Task HoldAsync(ItemEntity item)
    {
        Assert.True(_fixture.Sessions.TryGetByCharacterId(_aria.Id, out var session));

        return _fixture.Network.ExecuteOnLoopAsync(() => session.Set(ItemSessionKeys.Held, new HeldItem(item.Id)));
    }

    private void OnTheGround(ItemEntity item, int tilesAway)
    {
        Move(
            item,
            () => item.PlaceOnGround(
                _aria.Map,
                new Point3D(_aria.Location.X + tilesAway, _aria.Location.Y, _aria.Location.Z)
            )
        );
    }

    // Out of the item service and back, so that what it keeps by place follows the item.
    private void Move(ItemEntity item, Action place)
    {
        _items.Remove([item.Id]);
        place();
        _items.Add([item]);
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
