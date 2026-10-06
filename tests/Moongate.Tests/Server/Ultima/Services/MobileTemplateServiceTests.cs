using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileTemplateServiceTests
{
    [Fact]
    public void TryGet_Get_AndCount()
    {
        var service =
            new MobileTemplateService(new StubDataLoaderService().With(new MobileTemplate { Id = "orc", Body = 17 }));

        Assert.True(service.TryGet("orc", out var orc));
        Assert.Equal(17, orc!.Body);
        Assert.False(service.TryGet("ORC", out _));
        Assert.Equal(1, service.Count);
        Assert.Contains("'ettin'", Assert.Throws<KeyNotFoundException>(() => service.Get("ettin")).Message);
    }
}
