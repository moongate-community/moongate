using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.Books;
using Moongate.Server.Ultima.Packets.Gumps;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

public sealed class ReadableBookIntegrationTests
{
    // The shipped readable_book.lua: a double click opens the client's book, with no parchment.
    [Fact]
    public async Task ADoubleClickOnABook_SendsItsCoverAndPages()
    {
        await using var fixture = await BookLuaFixture.CreateAsync(realGumps: true);
        var documents = fixture.Documents;
        documents.Data.With(
            new BookTemplate { Id = "tome", Title = "Tome", Author = "Yorick", Content = "one\n\ntwo", ItemTemplate = "readable_book" }
        );

        await documents.OnLoopAsync(() =>
        {
            var tome = Assert.IsType<ItemEntity>(documents.Books.Give(documents.Player, "tome"));
            documents.World.Sender.Sent.Clear();

            var used = fixture.ItemScripts.Run(tome, "on_use", 2L);

            Assert.Equal(ScriptResultKind.Completed, used.Kind);
            Assert.Equal(true, Assert.Single(used.Values));
            Assert.Equal(2, Assert.Single(documents.World.Sender.Sent.OfType<BookHeaderPacket>()).PageCount);
            Assert.Equal(tome.Id, Assert.Single(documents.World.Sender.Sent.OfType<BookPagesPacket>()).Book);
        });
        await documents.OnLoopAsync(() => Assert.Empty(documents.World.Sender.Sent.OfType<CompressedGumpPacket>()));
        Assert.Empty(fixture.Errors);
    }
}
