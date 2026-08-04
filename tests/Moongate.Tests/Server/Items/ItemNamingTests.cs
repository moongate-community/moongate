using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;

namespace Moongate.Tests.Server.Items;

/// <summary>
/// What an item is called on a server-side surface. Almost nothing in UO stores a name — the client
/// is told a graphic and looks the word up itself — so an item with an empty Name is the normal case,
/// not a nameless one.
/// </summary>
public class ItemNamingTests
{
    private const int LongPants = 5433;

    [Fact]
    public void AnItemSomebodyRenamed_KeepsThatName()
        => Assert.Equal(
            "Excalibur",
            Clilocs().DisplayName(Item(LongPants, name: "Excalibur"), Template("long pants"))
        );

    [Fact]
    public void AnUnnamedItem_TakesItsTemplatesName()
        => Assert.Equal("long pants", Clilocs().DisplayName(Item(LongPants), Template("long pants")));

    /// <summary>
    /// The case that reported items as unnamed: no stored name, no template name, but the client
    /// files know the word perfectly well.
    /// </summary>
    [Fact]
    public void AnItemNamedOnlyByItsGraphic_TakesTheClientsWordForIt()
        => Assert.Equal("long pants", Clilocs("long pants").DisplayName(Item(LongPants), Template(null)));

    [Fact]
    public void WithNoClientFiles_TheTemplateIdIsBetterThanNothing()
        => Assert.Equal("long_pants", Clilocs().DisplayName(Item(LongPants), Template(null)));

    [Fact]
    public void WithNoTemplateAtAll_TheChainStillAnswers()
        => Assert.Equal("long pants", Clilocs("long pants").DisplayName(Item(LongPants), null));

    // A renamed item beats its template, and the template beats the client's word: most specific wins.
    [Fact]
    public void TheMostSpecificNameWins()
    {
        var clilocs = Clilocs("long pants");

        Assert.Equal("Excalibur", clilocs.DisplayName(Item(LongPants, name: "Excalibur"), Template("trousers")));
        Assert.Equal("trousers", clilocs.DisplayName(Item(LongPants), Template("trousers")));
    }

    private static StubClilocService Clilocs(string? text = null)
        => new(text);

    private static ItemEntity Item(int itemId, string name = "")
        => new() { Id = new(0x40000002), TemplateId = "long_pants", ItemId = itemId, Name = name };

    private static ItemTemplate? Template(string? name)
        => new() { Id = "long_pants", Name = name ?? string.Empty, ItemId = LongPants };
}
