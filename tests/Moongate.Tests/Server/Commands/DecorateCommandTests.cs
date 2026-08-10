using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.Signs;
using Moongate.UO.Data.Types;
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

    /// <summary>
    /// A catalogue with objects in it that places none and skips none has failed, whatever the cause —
    /// and "Decoration done: placed 0, skipped 0" reads exactly like a successful second run. This is
    /// the shape the missing-template bug took on a real shard: the reason was in the server log and
    /// the operator was told the job was done.
    /// </summary>
    [Fact]
    public void Execute_WhenNothingIsPlacedOrSkipped_ReportsFailureNotCompletion()
    {
        var (command, _) = Build([new DecorationGroup { ItemId = 100, At = [[10, 10, 0]] }], factoryReturnsNothing: true);
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.DoesNotContain("Decoration done", replies[^1]);
        Assert.Contains("server log", replies[^1]);
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

    /// <summary>A factory that builds nothing, standing in for any reason placement cannot produce an item.</summary>
    private sealed class BarrenItemFactory : IItemFactoryService
    {
        public IReadOnlyList<ItemEntity> CreateByCategory(string category, int count = 1, int amount = 1, Hue? hue = null)
            => [];

        public IReadOnlyList<ItemEntity> CreateByTag(string tag, int count = 1, int amount = 1, Hue? hue = null)
            => [];

        public IReadOnlyList<ItemEntity> CreateFromTemplate(string templateId, int count = 1, int amount = 1, Hue? hue = null)
            => [];
    }

    private static CommandContext Context(List<string> replies)
        => new(CommandSourceType.Console, null, [], replies.Add);

    [Fact]
    public void Execute_AnnouncesDecorationAndSignsTogether()
    {
        var (command, _) = Build(
            [new DecorationGroup { ItemId = 100, At = [[1, 1, 0]] }],
            signs:
            [
                new SignEntry { Map = MapType.Felucca, ItemId = 3032, X = 5, Y = 5, Z = 0, Label = "#1016093" }
            ]
        );
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.Equal("Placing 2 catalogued decoration object(s). The world pauses while it runs.", replies[0]);
    }

    // Signs alone are still something to place: a shard whose decoration is already down would
    // otherwise be told there was nothing to do.
    [Fact]
    public void Execute_NoDecorationButSomeSigns_DoesNotSayThereIsNothingToPlace()
    {
        var (command, _) = Build(
            [],
            signs:
            [
                new SignEntry { Map = MapType.Felucca, ItemId = 3032, X = 5, Y = 5, Z = 0, Label = "#1016093" }
            ]
        );
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.DoesNotContain("Nothing to place", replies[0]);
    }

    // The repair is the whole reason the number exists: an operator who never sees it has no way to
    // know their shard's doors were fixed, or that they needed fixing.
    [Fact]
    public void Execute_WhenDoorsAreConverted_SaysSo()
    {
        var groups = new[] { new DecorationGroup { Type = "MetalDoor", ItemId = 0x677, At = [[10, 10, 0]] } };
        var world = new World();

        Build(groups, world: world).Command.Execute(Context([]));

        var replies = new List<string>();

        Build(groups, world: world, withDoorTemplate: true).Command.Execute(Context(replies));

        Assert.Contains("Converted 1 already-placed door(s).", replies[^1]);
    }

    // A line about zero converted doors on every run is noise.
    [Fact]
    public void Execute_WhenNothingIsConverted_DoesNotMentionConversion()
    {
        var (command, _) = Build([new DecorationGroup { ItemId = 100, At = [[1, 1, 0]] }]);
        var replies = new List<string>();

        command.Execute(Context(replies));

        Assert.DoesNotContain("Converted", replies[^1]);
    }

    /// <summary>One world two commands can be built over, so the second sees what the first placed.</summary>
    private sealed class World
    {
        public FakePersistenceService Persistence { get; } = new();

        public SpatialIndexService? Spatial { get; set; }
    }

    private static (DecorateCommand Command, SpatialIndexService Spatial) Build(
        IEnumerable<DecorationGroup> groups,
        bool factoryReturnsNothing = false,
        IEnumerable<SignEntry>? signs = null,
        World? world = null,
        bool withDoorTemplate = false
    )
    {
        world ??= new();
        var persistence = world.Persistence;
        var spatial = world.Spatial ??= new SpatialIndexService(persistence, new StubLoopAffinity(), new StubEventBus());
        var items = new ItemService(persistence, spatial: spatial);

        var templates = new ItemTemplateService();
        templates.Register(
            new ItemTemplate
            {
                Id = DecorationPlacementService.TemplateId, Name = "", Category = "World", ItemId = 1
            }
        );

        if (withDoorTemplate)
        {
            templates.Register(
                new ItemTemplate
                {
                    Id = "metal_door", Name = "", Category = "Structure", ItemId = 0x675, ScriptId = "items.door"
                }
            );
        }

        var catalog = new DecorationCatalog();
        catalog.Add(1, groups);

        IItemFactoryService factory = factoryReturnsNothing
                                          ? new BarrenItemFactory()
                                          : new ItemFactoryService(templates, new(1));

        var signService = new SignService();

        foreach (var sign in signs ?? [])
        {
            signService.Register(sign);
        }

        return (
            new(catalog, new(catalog, factory, items, spatial, templates, signService), signService),
            spatial
        );
    }
}
