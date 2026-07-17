using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Containers;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Data;

/// <summary>
/// Exercises the gump chain against the real shipped assets — the item templates and the gump table
/// ported from ModernUO's containers.cfg — rather than hand-built fixtures. These are the numbers a
/// player's client actually receives.
/// </summary>
[Collection("ItemTemplateSeeding")]
public class ContainerTemplatesTests
{
    [Theory]
    // The template names its own gump, so it wins outright.
    [InlineData("backpack", 60)]
    [InlineData("elven_dresser_east_addon", 81)]
    [InlineData("elven_dresser_south_addon", 81)]
    [InlineData("arcane_bookshelf_east_addon", 263)]
    [InlineData("elven_wash_basin_east_addon", 260)]
    [InlineData("ornate_elven_chest_east_addon", 268)]
    [InlineData("boiling_cauldron_addon", 9)]
    [InlineData("closed_barrel", 62)]
    [InlineData("commodity_deed_box", 67)]
    [InlineData("gift_box_cube", 283)]
    [InlineData("gift_box_cylinder", 284)]
    [InlineData("gift_box_octogon", 285)]
    [InlineData("gift_box_angel", 287)]
    [InlineData("red_velvet_gift_box", 63)]
    [InlineData("treasure_chest_level1", 68)]
    [InlineData("treasure_chest_level4", 68)]
    // The template names none, so the gump table answers for the graphic — as ModernUO's
    // ContainerData.GetData(itemID) does for a class that leaves DefaultGumpID alone.
    [InlineData("basket1_artifact", 65)]
    [InlineData("basket2_artifact", 65)]
    [InlineData("basket3_north_artifact", 63)]
    [InlineData("basket5_west_artifact", 264)]
    [InlineData("basket6_artifact", 63)]
    [InlineData("chessboard", 2330)]
    [InlineData("backgammon", 2350)]
    // Neither names one: the plain bag, the same landing spot the backpack has.
    [InlineData("backpack_artifact", ContainerGumpLayout.DefaultGumpId)]
    public async Task ContainerTemplate_ResolvesToItsExpectedGump(string templateId, int expectedGumpId)
    {
        using var assets = await ShippedAssets.LoadAsync();

        Assert.Equal(expectedGumpId, assets.Subscriber.ResolveGumpId(assets.Item(templateId)));
    }

    [Theory]
    // Container graphics that this shard deliberately does not open. ModernUO agrees: each of these is
    // an `Item`/`AddonComponent`, not a Container, despite tiledata flagging the graphic.
    [InlineData("key_ring")]
    [InlineData("potion_keg")]
    [InlineData("bag_of_sending")]
    [InlineData("bulletin_board")]
    [InlineData("ballot_box")]
    [InlineData("small_urn")]
    [InlineData("fish_bowl")]
    // Spellbooks are container graphics too, and stay shut until the book work lands.
    [InlineData("spellbook")]
    [InlineData("necromancer_spellbook")]
    // An ordinary item, for contrast.
    [InlineData("dagger")]
    public async Task NonContainerTemplate_StaysShut(string templateId)
    {
        using var assets = await ShippedAssets.LoadAsync();

        Assert.Null(assets.Subscriber.ResolveGumpId(assets.Item(templateId)));
    }

    [Fact]
    public async Task EveryContainerTemplate_ResolvesToSomeGump()
    {
        using var assets = await ShippedAssets.LoadAsync();

        var containers = assets.Templates.All.Where(template => template.Container is not null).ToList();

        // The port declares a good number of them; a collapse to near-zero means the assets stopped loading.
        Assert.True(containers.Count > 50, $"Expected many container templates, got {containers.Count}");

        foreach (var template in containers)
        {
            Assert.NotNull(assets.Subscriber.ResolveGumpId(assets.Item(template.Id)));
        }
    }

    /// <summary>Seeds the embedded item templates and gump table into a throwaway root and wires the chain over them.</summary>
    private sealed class ShippedAssets : IDisposable
    {
        private readonly string _root;

        private ShippedAssets(string root, ItemTemplateService templates, ContainerSubscriber subscriber)
        {
            _root = root;
            Templates = templates;
            Subscriber = subscriber;
        }

        public ItemTemplateService Templates { get; }

        public ContainerSubscriber Subscriber { get; }

        public static async Task<ShippedAssets> LoadAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "mg-containers-" + Guid.NewGuid().ToString("N"));
            var directories = new DirectoriesConfig(root, []);
            var templates = new ItemTemplateService();
            var gumps = new ContainerGumpService();

            await new ItemTemplatesLoader(templates, directories).LoadAsync();
            await new ContainerGumpsLoader(gumps, directories).LoadAsync();

            var subscriber = new ContainerSubscriber(
                new StubSessionManager(),
                new StubItemService([]),
                templates,
                gumps,
                new OplService(new FakePersistenceService(), templates)
            );

            return new(root, templates, subscriber);
        }

        /// <summary>An item as the factory would build it from that template: the template id and the graphic.</summary>
        public ItemEntity Item(string templateId)
        {
            var template = Templates.GetById(templateId);
            Assert.True(template is not null, $"template '{templateId}' is not in the shipped assets");

            return new() { Id = new Serial(0x40000005), TemplateId = template!.Id, ItemId = template.ItemId, Amount = 1 };
        }

        public void Dispose()
            => Directory.Delete(_root, true);
    }
}
