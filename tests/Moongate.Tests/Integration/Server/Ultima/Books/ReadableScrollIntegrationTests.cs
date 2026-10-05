using Moongate.Core.Geometry;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Handlers.Items;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Titles;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

public sealed class ReadableScrollIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealJailScript_NewAndLegacyNotes_DisplaySavedPlainText(bool legacy)
    {
        await using var fixture = await BookLuaFixture.CreateAsync();
        var documents = fixture.Documents;
        await documents.OnLoopAsync(() =>
        {
            var note = documents.Give();
            note.TemplateId = "jail_release_note";
            note.SetProp("jail.text", "Legacy <note>");
            if (legacy)
            {
                note.RemoveProp("book.content");
            }
            documents.Items.PlaceOnGround(note, MapType.Trammel, new Point3D(1600, 1600, 0));
            var used = fixture.ItemScripts.Run(note, "on_use", 3L);
            Assert.Equal(ScriptResultKind.Completed, used.Kind);
            Assert.Equal(true, Assert.Single(used.Values));
            Assert.Empty(documents.Gumps.Opened);
        });
        await documents.OnLoopAsync(() =>
        {
            var strings = Assert.Single(documents.Gumps.Opened).Gump.Layout.Build().Strings;
            Assert.Contains(legacy ? "Legacy &lt;note&gt;" : "Dear Pippo,<br><br>Bring this to Vega.", strings);
        });
        Assert.Empty(fixture.Errors);
    }
    [Fact]
    public async Task RealLua_GiveAndDoubleClick_SendGumpOnNextLoopTurn()
    {
        await using var fixture = await BookLuaFixture.CreateAsync(realGumps: true);
        var documents = fixture.Documents;
        await documents.OnLoopAsync(() =>
        {
            var result = fixture.Engine.Call("make_letter");
            Assert.Equal(ScriptResultKind.Completed, result.Kind);
            var serial = Convert.ToUInt32(Assert.Single(result.Values));
            Assert.True(documents.Items.TryGet(new(serial), out var note));
            Assert.Equal("Dear Pippo,\n\nBring this to Vega.", note.GetProp<string>("book.content"));
            var used = fixture.ItemScripts.Run(note, "on_use", 2L);
            Assert.Equal(ScriptResultKind.Completed, used.Kind);
            Assert.Equal(true, Assert.Single(used.Values));
            Assert.Empty(documents.World.Sender.Sent.OfType<CompressedGumpPacket>());
        });
        await documents.OnLoopAsync(() => Assert.Single(documents.World.Sender.Sent.OfType<CompressedGumpPacket>()));
        Assert.Empty(fixture.Errors);
    }

    [Fact]
    public async Task RealDoubleClick_ScrollInNearbyChest_SendsSavedTextOnNextLoopTurn()
    {
        await using var fixture = await BookLuaFixture.CreateAsync(realGumps: true);
        var documents = fixture.Documents;
        await documents.OnLoopAsync(() =>
        {
            var note = documents.Give();
            var chest = new ItemEntity { Id = new(0x40000020), ItemId = 0x0E43, Amount = 1 };
            documents.Items.Add([chest]);
            documents.Items.PlaceOnGround(chest, MapType.Trammel, new Point3D(1601, 1600, 0));
            documents.Items.MoveToContainer(note, chest.Id, new Point2D(20, 20));
            var handler = new UseRequestPacketHandler(
                documents.Items, documents.World.Mobiles, documents.Data, new WorldConfig(), new FakeTileDataService(),
                new ContainerLayoutService(documents.Data), documents.World.Sender,
                TestTooltips.Create(documents.Items, documents.World.Mobiles), new FameKarmaTitleService(documents.Data),
                fixture.ItemScripts, documents.Bank);

            handler.Handle(documents.Session, new UseRequestPacket { Target = note.Id });

            Assert.Empty(documents.World.Sender.Sent.OfType<CompressedGumpPacket>());
        });
        await documents.OnLoopAsync(() => Assert.Single(documents.World.Sender.Sent.OfType<CompressedGumpPacket>()));
        Assert.Empty(fixture.Errors);
    }

    [Fact]
    public async Task RealLua_MissingCustomValue_ReturnsNilWithoutItemOrSerial()
    {
        await using var fixture = await BookLuaFixture.CreateAsync();
        await fixture.Documents.OnLoopAsync(() =>
        {
            var result = fixture.Engine.Call("missing_values");
            Assert.Equal(ScriptResultKind.Completed, result.Kind);
            Assert.Null(Assert.Single(result.Values));
            Assert.Single(fixture.Documents.Serials.Serials);
            Assert.Single(fixture.Documents.Items.Items);
        });
        Assert.Empty(fixture.Errors);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("moved")]
    [InlineData("session")]
    public async Task DeferredOpen_StateChangesBeforeDelivery_SendsNothing(string change)
    {
        await using var fixture = await BookLuaFixture.CreateAsync();
        var documents = fixture.Documents;
        await documents.OnLoopAsync(() =>
        {
            var note = documents.Give();
            var result = fixture.Engine.Call("use_letter", (long)note.Id.Value);
            Assert.Equal(ScriptResultKind.Completed, result.Kind);
            Assert.Equal(true, Assert.Single(result.Values));
            Assert.Empty(documents.Gumps.Opened);
            if (change == "removed") documents.Items.Remove([note.Id]);
            if (change == "moved") documents.Items.PlaceOnGround(note, MapType.Trammel, new Point3D(1700, 1700, 0));
            if (change == "session")
            {
                documents.World.Sessions.Remove(documents.Session.SessionId);
                var replacement = documents.World.Sessions.GetOrCreate(documents.Session.NetworkSession.Client!);
                replacement.Set(SessionKeys.CharacterId, documents.Player.Id);
            }
        });
        await documents.OnLoopAsync(() => Assert.Empty(documents.Gumps.Opened));
        Assert.Empty(fixture.Errors);
    }
}
