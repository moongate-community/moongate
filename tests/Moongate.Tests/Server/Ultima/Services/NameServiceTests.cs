using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NameServiceTests
{
    [Fact]
    public void RandomName_ComesFromTheList_IgnoringIdCase()
    {
        var service = CreateService();

        Assert.All(Enumerable.Range(0, 50), _ => Assert.Contains(service.RandomName("MALE"), new[] { "Aaron", "Abbot" }));
        Assert.Equal("Abghat", service.RandomName("orc"));
    }

    [Fact]
    public void HasList_And_RandomName_UnknownId()
    {
        var service = CreateService();

        Assert.True(service.HasList("orc"));
        Assert.False(service.HasList("elf"));
        Assert.Contains("'elf'", Assert.Throws<KeyNotFoundException>(() => service.RandomName("elf")).Message);
    }

    private static NameService CreateService()
    {
        return new(
            new StubDataLoaderService().With(
                new NameList { Id = "male", Names = ["Aaron", "Abbot"] },
                new NameList { Id = "orc", Names = ["Abghat"] }
            )
        );
    }
}
