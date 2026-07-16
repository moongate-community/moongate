using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Names;

namespace Moongate.Tests.Server;

public class NameServiceTests
{
    [Fact]
    public void All_IsOrderedByType()
    {
        var service = new NameService();
        service.Register(List("male"));
        service.Register(List("female"));

        Assert.Equal(new[] { "female", "male" }, service.All.Select(l => l.Type).ToArray());
    }

    [Fact]
    public void GetByType_Unknown_ReturnsNull()
    {
        var service = new NameService();

        Assert.Null(service.GetByType("nope"));
    }

    [Fact]
    public void Register_ThenGetByType_IsCaseInsensitive()
    {
        var service = new NameService();
        service.Register(List("orc", "Grok", "Zug"));

        Assert.Equal(1, service.Count);
        Assert.Equal(2, service.GetByType("ORC")!.Names.Count);
    }

    private static NameList List(string type, params string[] names)
        => new() { Type = type, Names = [.. names] };
}
