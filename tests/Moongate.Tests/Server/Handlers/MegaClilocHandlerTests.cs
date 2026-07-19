using Moongate.Persistence.Entities;
using Moongate.Server.Handlers;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Handlers;

public class MegaClilocHandlerTests
{
    [Fact]
    public void BuildResponses_KnownSerial_YieldsPacketWithSnapshotContent()
    {
        var (opl, items) = Build();
        var item = NewItem(items, "a dagger");

        var responses = MegaClilocHandler.BuildResponses([item.Id], opl).ToList();

        var response = Assert.Single(responses);
        Assert.Equal(item.Id, response.Serial);
        Assert.Equal(opl.GetOrBuild(item.Id).Hash, response.Hash);
        Assert.Equal("a dagger", response.Entries[0].Arguments);
    }

    [Fact]
    public void BuildResponses_MixedBatch_AnswersOnlyKnownInOrder()
    {
        var (opl, items) = Build();
        var first = NewItem(items, "a dagger");
        var second = NewItem(items, "a katana");

        var responses = MegaClilocHandler
                        .BuildResponses([first.Id, new(0x40009999), second.Id], opl)
                        .ToList();

        Assert.Equal(2, responses.Count);
        Assert.Equal(first.Id, responses[0].Serial);
        Assert.Equal(second.Id, responses[1].Serial);
    }

    [Fact]
    public void BuildResponses_UnknownSerial_YieldsNothing()
    {
        var (opl, _) = Build();

        Assert.Empty(MegaClilocHandler.BuildResponses([new(0x40009999)], opl));
    }

    private static (OplService Opl, ItemService Items) Build()
    {
        var persistence = new FakePersistenceService();

        return (new(persistence, new ItemTemplateService()), new(persistence));
    }

    private static ItemEntity NewItem(ItemService items, string name)
    {
        var item = new ItemEntity { Name = name, ItemId = 3921 };
        items.Create(item);

        return item;
    }
}
