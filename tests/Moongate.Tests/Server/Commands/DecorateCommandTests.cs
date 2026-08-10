using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.World;

namespace Moongate.Tests.Server.Commands;

/// <summary>
/// The command that turns the catalogue into world. The placing belongs to
/// <see cref="DecorationPlacementService" /> and is tested there; what is held to account here is
/// what only the command decides — that it reaches the world at all, and that what it says back
/// distinguishes a first run from a second one and from a catalogue that never loaded.
/// </summary>
public class DecorateCommandTests
{
    [Fact]
    public void Execute_PlacesTheCatalogueIntoTheWorld()
    {
        var (command, spatial) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0], [11, 10, 0]] }]);
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.Equal(2, spatial.GetItemsInRange(1, new(10, 10, 0), 5).Count);
        Assert.Contains("placed 2, skipped 0", replies[^1]);
    }

    // The operator is told the size of the job before the world stops answering for it.
    [Fact]
    public void Execute_AnnouncesTheCountBeforeItStarts()
    {
        var (command, _) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0]] }]);
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.Equal(
            "Placing 1 catalogued decoration object(s). The world pauses while it runs.",
            replies[0]
        );
    }

    // Typing it twice is the expected accident, so the second run has to say it did nothing.
    [Fact]
    public void Execute_RunTwice_ReportsTheSecondRunAsSkipped()
    {
        var (command, spatial) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0], [11, 10, 0]] }]);
        var replies = new List<string>();

        command.Execute(Context(replies));
        replies.Clear();
        command.Execute(Context(replies));

        Assert.Contains("placed 0, skipped 2", replies[^1]);
        Assert.Equal(2, spatial.GetItemsInRange(1, new(10, 10, 0), 5).Count);
    }

    // Nothing loaded is a broken data loader, not an idempotent second run, and "placed 0" alone
    // cannot tell you which one you are looking at.
    [Fact]
    public void Execute_AnEmptyCatalogue_SaysSoRatherThanReportingZeroPlaced()
    {
        var (command, _) = Build([]);
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.Equal("Nothing to place: the decoration catalogue is empty.", Assert.Single(replies));
    }

    private static CommandContext Context(List<string> replies)
        => new(CommandSourceType.Console, null, [], replies.Add);

    private static (DecorateCommand Command, SpatialIndexService Spatial) Build(IEnumerable<DecorationGroup> groups)
    {
        var persistence = new FakePersistenceService();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), new StubEventBus());
        var items = new ItemService(persistence, spatial: spatial);

        var templates = new ItemTemplateService();
        templates.Register(
            new ItemTemplate
            {
                Id = DecorationPlacementService.TemplateId, Name = "", Category = "World", ItemId = 1
            }
        );

        var catalog = new DecorationCatalog();
        catalog.Add(1, groups);

        return (
            new(catalog, new(catalog, new ItemFactoryService(templates, new(1)), items, spatial)),
            spatial
        );
    }
}
