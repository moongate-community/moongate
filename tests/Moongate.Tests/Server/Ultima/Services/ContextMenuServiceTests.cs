using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.ContextMenus;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.ContextMenus;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ContextMenuServiceTests : IAsyncLifetime
{
    private const int Paperdoll = 3006123;
    private const int Backpack = 3006145;
    private const int Bank = 3006105;

    private static readonly Serial Banker = new(0x100);

    private readonly ItemService _items = TestItems.Create();
    private readonly RecordingUseService _use = new();
    private readonly RecordingNpcScriptService _npcScripts = new();
    private readonly RecordingItemScriptService _itemScripts = new();
    private readonly StubBankService _bank = new();

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _banker = null!;
    private ContextMenuService _menus = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        _aria = aria;
        _aria.AccountId = new Serial(1002);
        _aria.Body = 400;
        _aria.Location = new Point3D(1600, 1600, 0);
        _banker = new()
        {
            Id = Banker, Name = "a banker", TemplateId = "banker", Body = 400, Map = MapType.Trammel,
            Location = new Point3D(1605, 1600, 0)
        };
        _fixture.Mobiles.EnterWorld(_banker);
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([_backpack]);
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
                new ItemTemplate { Id = "stone", ItemId = new Serial(0x0ED4) },
                new ItemTemplate { Id = "staff_marker", ItemId = new Serial(0x1BC3), Visibility = AccountType.GameMaster }
            )
        );
        _menus = new(
            _items,
            _fixture.Mobiles,
            templates,
            _fixture.Sender,
            _use,
            new WorldConfig(),
            _npcScripts,
            _itemScripts,
            _bank
        );
    }

    [Fact]
    public void Request_OnAHumanMobile_OffersItsPaperdoll()
    {
        Assert.True(Request(_session, Banker));

        var menu = Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>());
        Assert.Equal(Banker, menu.Target);
        Assert.Equal([(Paperdoll, false)], menu.Entries);
    }

    // An animal has no paperdoll, and nothing else to offer: no menu at all.
    [Fact]
    public void Request_OnAMobileWithNothingToOffer_SendsNothing()
    {
        _banker.Body = 0xC9;

        Assert.False(Request(_session, Banker));

        Assert.Empty(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>());
    }

    [Fact]
    public void Request_OnOneself_OffersPaperdollAndBackpack_ButNoBackpackToAGhostOrOnAnother()
    {
        Assert.True(Request(_session, _aria.Id));
        Assert.Equal(
            [(Paperdoll, false), (Backpack, false)],
            _fixture.Sender.Sent.OfType<DisplayContextMenuPacket>().Last().Entries
        );

        // A ghost keeps its paperdoll and has no backpack to open.
        _aria.Body = 402;
        _use.PaperdollBodies.Add(402);
        Assert.True(Request(_session, _aria.Id));
        Assert.Equal([(Paperdoll, false)], _fixture.Sender.Sent.OfType<DisplayContextMenuPacket>().Last().Entries);
    }

    // The entries of the script come after the server's, with the range and the state the script gave them.
    [Fact]
    public void Request_OnAnNpcWithAScript_AddsItsEntriesAfterTheServers()
    {
        _npcScripts.Result = ScriptResult.Completed(
            [Entries(Entry("bank", Bank, range: 12), Entry("far", 3006103, range: 2), Entry("off", 3006104, enabled: false))]
        );

        Assert.True(Request(_session, Banker));

        Assert.Equal([$"Run {Banker.Value} on_context_menu 2"], _npcScripts.Calls);
        // Five tiles away: within 12, not within 2.
        Assert.Equal(
            [(Paperdoll, false), (Bank, false), (3006103, true), (3006104, true)],
            Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries
        );
    }

    [Fact]
    public void Request_OnAnItemWithAScript_OffersItsEntries_AndNoneWithoutOne()
    {
        var stone = Stone(1601);
        Assert.False(Request(_session, stone.Id));

        _itemScripts.Scripted.Add("stone");
        _itemScripts.Result = ScriptResult.Completed([Entries(Entry("touch", 3006150))]);

        Assert.True(Request(_session, stone.Id));
        Assert.Equal([(3006150, false)], Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries);
    }

    // What a script answers is not trusted: only well formed entries are taken, and nothing throws.
    [Fact]
    public void Request_AScriptThatAnswersBadly_LeavesTheBadEntriesOut()
    {
        var bad = new LuaTable();
        bad[1] = 42;
        bad[2] = "bank";
        bad[3] = Entry(null, Bank);
        bad[4] = Entry("zero", 0);
        bad[5] = Entry("far", Bank, range: 500);
        bad[6] = Entry("negative", Bank, range: -1);
        bad[7] = Entry("fraction", 3006105.5);
        bad[8] = Entry("good", Bank);
        _npcScripts.Result = ScriptResult.Completed([bad]);

        Assert.True(Request(_session, Banker));

        Assert.Equal(
            [(Paperdoll, false), (Bank, false)],
            Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries
        );
    }

    [Theory]
    [InlineData("number")]
    [InlineData("string")]
    [InlineData("nothing")]
    [InlineData("waits")]
    [InlineData("missing")]
    public void Request_AScriptThatAnswersNoList_AddsNothing(string answer)
    {
        _npcScripts.Result = answer switch
        {
            "number"  => ScriptResult.Completed([42L]),
            "string"  => ScriptResult.Completed(["bank"]),
            "nothing" => ScriptResult.Completed([]),
            "waits"   => ScriptResult.Suspended,
            _         => ScriptResult.Missing
        };

        Assert.True(Request(_session, Banker));

        Assert.Equal([(Paperdoll, false)], Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries);
    }

    [Fact]
    public void Request_MoreEntriesThanAMenuHolds_KeepsTheFirst()
    {
        var many = new LuaTable();

        for (var index = 1; index <= 30; index++)
        {
            many[index] = Entry($"e{index}", 3006100 + index);
        }

        _npcScripts.Result = ScriptResult.Completed([many]);

        Assert.True(Request(_session, Banker));

        Assert.Equal(
            ContextMenuService.MaxEntries,
            Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries.Count
        );
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("too far")]
    [InlineData("other map")]
    [InlineData("hidden")]
    [InlineData("staff item")]
    [InlineData("carried by another")]
    public void Request_OnWhatThePlayerCannotReachOrSee_SendsNothing(string what)
    {
        var target = Banker;

        switch (what)
        {
            case "unknown":
                target = new Serial(0x999);
                break;
            case "too far":
                _banker.Location = new Point3D(1619, 1600, 0);
                break;
            case "other map":
                _banker.Map = MapType.Felucca;
                break;
            case "hidden":
                _banker.Hidden = true;
                break;
            case "staff item":
                target = Item("staff_marker", 1601).Id;
                _itemScripts.Scripted.Add("staff_marker");
                _itemScripts.Result = ScriptResult.Completed([Entries(Entry("x", Bank))]);
                break;
            default:
                var theirs = new ItemEntity
                    { Id = new Serial(0x40000050), TemplateId = "stone", ItemId = 0x0ED4, Amount = 1 };
                theirs.Equip(Banker, LayerType.OneHanded);
                _items.Add([theirs]);
                _itemScripts.Scripted.Add("stone");
                _itemScripts.Result = ScriptResult.Completed([Entries(Entry("x", Bank))]);
                target = theirs.Id;
                break;
        }

        Assert.False(Request(_session, target));

        Assert.Empty(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>());
    }

    // The dead are answered by nobody: a ghost keeps the paperdolls, and no script offers it anything.
    [Fact]
    public void Request_ByAGhost_GetsNoEntryOfAScript()
    {
        _npcScripts.Result = ScriptResult.Completed([Entries(Entry("bank", Bank, range: 12))]);
        _aria.Body = 402;

        Assert.True(Request(_session, Banker));

        Assert.Empty(_npcScripts.Calls);
        Assert.Equal([(Paperdoll, false)], Assert.Single(_fixture.Sender.Sent.OfType<DisplayContextMenuPacket>()).Entries);
    }

    // Dead between the menu and the choice: the entry of a script is not run.
    [Fact]
    public void Select_AScriptEntry_ByWhoDiedMeanwhile_RunsNothing()
    {
        _npcScripts.Result = ScriptResult.Completed([Entries(Entry("bank", Bank, range: 12))]);
        Assert.True(Request(_session, Banker));
        _npcScripts.Calls.Clear();
        _aria.Body = 402;

        Assert.False(Select(_session, Banker, 1));

        Assert.Empty(_npcScripts.Calls);
    }

    // What lies in a bank that is not open is out of reach, for a menu as for a double click.
    [Fact]
    public void Request_OnAnItemInTheClosedBank_SendsNothing_AndInTheOpenOneItDoes()
    {
        var box = new ItemEntity { Id = new Serial(0x40000060), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        box.Equip(_aria.Id, LayerType.Bank);
        var stone = new ItemEntity { Id = new Serial(0x40000061), TemplateId = "stone", ItemId = 0x0ED4, Amount = 1 };
        stone.PutInContainer(box.Id, new Point2D(10, 10));
        _items.Add([box, stone]);
        _itemScripts.Scripted.Add("stone");
        _itemScripts.Result = ScriptResult.Completed([Entries(Entry("touch", 3006150))]);
        _bank.Locked.Add(stone.Id);

        Assert.False(Request(_session, stone.Id));
        Assert.Empty(_itemScripts.Calls);

        _bank.Locked.Clear();

        Assert.True(Request(_session, stone.Id));
    }

    // Lifted onto a cursor, an item of the ground is not there to click.
    [Fact]
    public void Request_OnAnItemLiftedFromTheGround_SendsNothing_AndAChoiceOfItRunsNothing()
    {
        var stone = Stone(1601);
        _itemScripts.Scripted.Add("stone");
        _itemScripts.Result = ScriptResult.Completed([Entries(Entry("touch", 3006150))]);
        Assert.True(Request(_session, stone.Id));
        _items.Hide(stone);

        Assert.False(Select(_session, stone.Id, 0));
        Assert.False(Request(_session, stone.Id));

        Assert.Empty(_itemScripts.Queued);
    }

    // A request that offers nothing still takes the place of the menu before it.
    [Fact]
    public void Request_ThatOffersNothing_ForgetsTheMenuKept()
    {
        Assert.True(Request(_session, Banker));
        Assert.False(Request(_session, new Serial(0x999)));

        Assert.False(Select(_session, Banker, 0));
        Assert.Empty(_use.Used);
    }

    [Fact]
    public void Select_AServerEntry_DoesWhatADoubleClickDoes()
    {
        Assert.True(Request(_session, _aria.Id));

        Assert.True(Select(_session, _aria.Id, 1));
        Assert.Equal((_session, _backpack.Id), Assert.Single(_use.Used));

        Assert.True(Request(_session, Banker));
        Assert.True(Select(_session, Banker, 0));
        Assert.Equal((_session, Banker), _use.Used[1]);
    }

    [Fact]
    public void Select_AScriptEntry_TellsTheScriptWhichOne()
    {
        _npcScripts.Result = ScriptResult.Completed([Entries(Entry("bank", Bank, range: 12))]);
        Assert.True(Request(_session, Banker));
        _npcScripts.Calls.Clear();

        Assert.True(Select(_session, Banker, 1));

        Assert.Equal([$"Queue {Banker.Value} on_context_menu_select 2 bank"], _npcScripts.Calls);
        Assert.Empty(_use.Used);
    }

    [Fact]
    public void Select_AnItemsScriptEntry_TellsTheItemsScript()
    {
        var stone = Stone(1601);
        _itemScripts.Scripted.Add("stone");
        _itemScripts.Result = ScriptResult.Completed([Entries(Entry("touch", 3006150))]);
        Assert.True(Request(_session, stone.Id));

        Assert.True(Select(_session, stone.Id, 0));

        Assert.Equal([$"0x{stone.Id.Value:X8} on_context_menu_select 2 touch"], _itemScripts.Queued);
    }

    // The Enhanced Client's own icons, such as the bank on a banker's status bar, name an entry by a fixed number
    // instead of its place in the menu: 0x78 is "Open Bank Box", wherever the menu has it.
    [Fact]
    public void Select_AFixedIndexOfTheEnhancedClient_ChoosesTheEntryWithThatText()
    {
        _npcScripts.Result = ScriptResult.Completed([Entries(Entry("other", 3006150), Entry("bank", Bank, range: 12))]);
        Assert.True(Request(_session, Banker));
        _npcScripts.Calls.Clear();

        Assert.True(Select(_session, Banker, 0x78));

        Assert.Equal([$"Queue {Banker.Value} on_context_menu_select 2 bank"], _npcScripts.Calls);
    }

    // A fixed number chooses only what the menu offered, and only what can be chosen.
    [Theory]
    [InlineData("not offered")]
    [InlineData("unknown number")]
    [InlineData("greyed")]
    [InlineData("out of range")]
    public void Select_AFixedIndexOfTheEnhancedClient_ForWhatCannotBeChosen_RunsNothing(string what)
    {
        _npcScripts.Result = what switch
        {
            "greyed"       => ScriptResult.Completed([Entries(Entry("bank", Bank, enabled: false))]),
            "out of range" => ScriptResult.Completed([Entries(Entry("bank", Bank, range: 2))]),
            _              => ScriptResult.Completed([Entries(Entry("bank", Bank, range: 12))])
        };
        Assert.True(Request(_session, Banker));
        _npcScripts.Calls.Clear();

        // 0x12D is "Tame", which this menu does not have; 0x7FF is no number the client uses.
        var index = what switch { "not offered" => 0x12D, "unknown number" => 0x7FF, _ => 0x78 };

        Assert.False(Select(_session, Banker, index));

        Assert.Empty(_npcScripts.Calls);
        Assert.Empty(_use.Used);
    }

    // The menu is good for one choice: a second one, or one with no menu, runs nothing.
    [Fact]
    public void Select_Twice_OrWithNoMenu_RunsNothingTheSecondTime()
    {
        Assert.False(Select(_session, Banker, 0));

        Assert.True(Request(_session, Banker));
        Assert.True(Select(_session, Banker, 0));
        Assert.False(Select(_session, Banker, 0));

        Assert.Single(_use.Used);
    }

    [Theory]
    [InlineData("another target")]
    [InlineData("index beyond")]
    [InlineData("negative index")]
    [InlineData("greyed")]
    [InlineData("walked away")]
    [InlineData("target gone")]
    [InlineData("target hidden")]
    [InlineData("target on another map")]
    public void Select_WhatTheMenuDidNotOffer_OrNoLongerHolds_RunsNothing(string what)
    {
        _npcScripts.Result =
            ScriptResult.Completed([Entries(Entry("bank", Bank, range: 6), Entry("off", 3006104, enabled: false))]);
        Assert.True(Request(_session, Banker));
        _npcScripts.Calls.Clear();
        var target = Banker;
        var index = 1;

        switch (what)
        {
            case "another target":
                target = _aria.Id;
                break;
            case "index beyond":
                index = 3;
                break;
            case "negative index":
                index = -1;
                break;
            case "greyed":
                index = 2;
                break;
            case "walked away":
                _aria.Location = new Point3D(1590, 1600, 0);
                break;
            case "target gone":
                _fixture.Mobiles.LeaveWorld(_banker.Id);
                break;
            case "target hidden":
                _banker.Hidden = true;
                break;
            default:
                _banker.Map = MapType.Felucca;
                break;
        }

        Assert.False(Select(_session, target, index));

        Assert.Empty(_npcScripts.Calls);
        Assert.Empty(_use.Used);
        // Whatever was wrong, the menu is spent.
        Assert.False(Select(_session, Banker, 1));
    }

    // A new menu takes the place of the one before: its indexes are the ones that count.
    [Fact]
    public void Request_Again_ReplacesTheMenuKept()
    {
        Assert.True(Request(_session, _aria.Id));
        Assert.True(Request(_session, Banker));

        Assert.False(Select(_session, _aria.Id, 1));
        Assert.Empty(_use.Used);
    }

    // Session state changes on the game loop only.
    private bool Request(GameSession session, Serial target)
    {
        var result = false;
        _fixture.Network.ExecuteOnLoopAsync(() => result = _menus.Request(session, target)).GetAwaiter().GetResult();

        return result;
    }

    private bool Select(GameSession session, Serial target, int index)
    {
        var result = false;
        _fixture.Network.ExecuteOnLoopAsync(() => result = _menus.Select(session, target, index)).GetAwaiter().GetResult();

        return result;
    }

    private ItemEntity Stone(int x)
    {
        return Item("stone", x);
    }

    private ItemEntity Item(string template, int x)
    {
        var item = new ItemEntity
            { Id = new Serial(0x40000010 + (uint)x), TemplateId = template, ItemId = 0x0ED4, Amount = 1 };
        item.PlaceOnGround(MapType.Trammel, new Point3D(x, 1600, 0));
        _items.Add([item]);

        return item;
    }

    private static LuaTable Entries(params LuaTable[] entries)
    {
        var table = new LuaTable();

        for (var index = 0; index < entries.Length; index++)
        {
            table[index + 1] = entries[index];
        }

        return table;
    }

    private static LuaTable Entry(string? id, double cliloc, double? range = null, bool? enabled = null)
    {
        var entry = new LuaTable();

        if (id is not null)
        {
            entry["id"] = id;
        }

        entry["cliloc"] = cliloc;

        if (range is { } tiles)
        {
            entry["range"] = tiles;
        }

        if (enabled is { } state)
        {
            entry["enabled"] = state;
        }

        return entry;
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
