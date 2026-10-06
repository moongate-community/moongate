using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Services.Motd;

namespace Moongate.Tests.Server.Ultima.Services.Motd;

public sealed class MotdRendererTests
{
    [Fact]
    public async Task RenderAsync_ReplacesAllBuiltinsExactly()
    {
        var registry = new MotdVariableRegistry();
        MotdRenderer.RegisterBuiltins(registry);
        var renderer = new MotdRenderer(registry);
        var context = new MotdContext("Shard", "Felucca", "1.2.3", "Moonrise", "Aria", 42);
        var line = new MotdLine(1, "${version}|${codename}|${server_name}|${realm_name}|${player_name}|${users_online}");

        Assert.Equal(
            "1.2.3|Moonrise|Shard|Felucca|Aria|42",
            await renderer.RenderAsync(line, context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task RenderAsync_PreservesLiteralsAndDoesNotExpandResolverOutput()
    {
        var registry = new MotdVariableRegistry();
        MotdRenderer.RegisterBuiltins(registry);
        registry.Register("custom", (_, _) => ValueTask.FromResult("${version}"));
        var renderer = new MotdRenderer(registry);
        var context = new MotdContext("S", "R", "1", "C", "P", 1);

        var result = await renderer.RenderAsync(
            new(1, "${custom} $ {x} ${version} ${version}"),
            context,
            CancellationToken.None
        );

        Assert.Equal("${version} $ {x} 1 1", result);
    }
}
