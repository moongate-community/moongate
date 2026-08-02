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

    private static (OplService Service, ItemEntity Item) Build(
        string templateName,
        string itemName,
        int amount,
        IClilocService? clilocs = null
    )
    {
        var persistence = new FakePersistenceService();
        var templates = new ItemTemplateService();

        templates.Register(new() { Id = "gold", Name = templateName, Category = "Misc", ItemId = GoldItemId });

        var item = new ItemEntity
        {
            TemplateId = "gold", ItemId = GoldItemId, Amount = amount, Name = itemName
        };

        persistence.Store<ItemEntity>().UpsertAsync(item).WaitSync();

        return (new(persistence, templates, clilocs ?? new StubClilocService("gold coin")), item);
    }
}
