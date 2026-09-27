using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class ItemTemplateServiceTests
{
    [Fact]
    public void TryGet_Get_AndCount()
    {
        var service = new ItemTemplateService(
            new StubDataLoaderService().With(new ItemTemplate { Id = "robe", ItemId = new Serial(0x1F03) })
        );

        Assert.True(service.TryGet("robe", out var robe));
        Assert.Equal(0x1F03u, robe!.ItemId.Value);
        Assert.False(service.TryGet("ROBE", out _));
        Assert.Equal(1, service.Count);
        Assert.Contains("'cape'", Assert.Throws<KeyNotFoundException>(() => service.Get("cape")).Message);
    }
}
