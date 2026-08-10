using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Localization;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.World;

/// <summary>
/// What names an item on a tooltip: our text when the shard has something to say, and the client's
/// own cliloc when it does not — which is what gets the name translated and the plural right.
/// </summary>
public class OplServiceNameTests
{
    private const int GoldItemId = 3821;
    private const int GoldCliloc = 1023821;  // ItemClilocs.ForItemId(3821)
    private const int StackCliloc = 1050039; // ~1_NUMBER~ ~2_ITEMNAME~
    private const int SignCliloc = 1016093;  // "Britain Bank", as a sign would carry it

    [Fact]
    public void NamedStack_KeepsItsTextArgument()
    {
        var (service, item) = Build("Ghost Ship Anchor", "", 5);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Equal(StackCliloc, entry.Cliloc);
        Assert.Equal("5\tGhost Ship Anchor", entry.Arguments);
    }

    [Fact]
    public void NamedTemplate_KeepsItsText()
    {
        var (service, item) = Build("Ghost Ship Anchor", "", 1);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Contains("Ghost Ship Anchor", entry.Arguments);
    }

    // A rename on the entity beats everything, which is already true and must stay true.
    [Fact]
    public void RenamedItem_BeatsTheTemplateAndTheCliloc()
    {
        var (service, item) = Build("Ghost Ship Anchor", "Squid's Anchor", 1);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Contains("Squid's Anchor", entry.Arguments);
    }

    [Fact]
    public void UnnamedSingle_IsTheItemsOwnCliloc()
    {
        var (service, item) = Build("", "", 1);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Equal(GoldCliloc, entry.Cliloc);
        Assert.Equal(string.Empty, entry.Arguments);
    }

    // The hash is what tells the client the argument is another cliloc to resolve, not literal text.
    [Fact]
    public void UnnamedStack_ReferencesTheClilocInsideTheStackLine()
    {
        var (service, item) = Build("", "", 1000);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Equal(StackCliloc, entry.Cliloc);
        Assert.Equal($"1000\t#{GoldCliloc}", entry.Arguments);
    }

    // No name and no cliloc: the template id is more use than the word "item".
    [Fact]
    public void UnnamedWithNoCliloc_FallsBackToTheTemplateId()
    {
        var (service, item) = Build("", "", 1, new StubClilocService());

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Contains("gold", entry.Arguments);
    }

    // A sign's cliloc IS its text, so it beats the one the graphic implies -- the graphic is a
    // signpost, and every signpost in the world shares it.
    [Fact]
    public void ItemWithItsOwnNameCliloc_IsNamedByThatCliloc()
    {
        var (service, item) = Build(
            "",
            "",
            1,
            StubClilocService.Entries((GoldCliloc, "gold coin"), (SignCliloc, "Britain Bank")),
            SignCliloc
        );

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Equal(SignCliloc, entry.Cliloc);
        Assert.Equal(string.Empty, entry.Arguments);
    }

    // The guard that matters more than the one above: this field is new on every item in the world,
    // and an item without one has to be named exactly as it was before it existed.
    [Fact]
    public void ItemWithoutANameCliloc_IsNamedExactlyAsBefore()
    {
        var (service, item) = Build("", "", 1);

        var entry = service.GetOrBuild(item.Id).Entries[0];

        Assert.Equal(GoldCliloc, entry.Cliloc);
        Assert.Equal(string.Empty, entry.Arguments);
    }

    // A shard that renamed the thing still wins: a name is a deliberate act, a cliloc is data.
    [Fact]
    public void ARenamedItem_BeatsItsOwnNameCliloc()
    {
        var (service, item) = Build(
            "",
            "Squid's Anchor",
            1,
            StubClilocService.Entries((SignCliloc, "Britain Bank")),
            SignCliloc
        );

        Assert.Contains("Squid's Anchor", service.GetOrBuild(item.Id).Entries[0].Arguments);
    }

    private static (OplService Service, ItemEntity Item) Build(
        string templateName,
        string itemName,
        int amount,
        IClilocService? clilocs = null,
        int nameCliloc = 0
    )
    {
        var persistence = new FakePersistenceService();
        var templates = new ItemTemplateService();

        templates.Register(new() { Id = "gold", Name = templateName, Category = "Misc", ItemId = GoldItemId });

        var item = new ItemEntity
        {
            TemplateId = "gold", ItemId = GoldItemId, Amount = amount, Name = itemName, NameCliloc = nameCliloc
        };

        persistence.Store<ItemEntity>().UpsertAsync(item).WaitSync();

        return (new(persistence, templates, clilocs ?? new StubClilocService("gold coin")), item);
    }
}
