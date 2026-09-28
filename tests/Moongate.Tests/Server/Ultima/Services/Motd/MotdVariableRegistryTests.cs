using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Services.Motd;

namespace Moongate.Tests.Server.Ultima.Services.Motd;

public sealed class MotdVariableRegistryTests
{
    private static readonly MotdContext Context = new("Moongate", "Felucca", "1.0.0", "Dawn", "Aria", 3);

    [Fact]
    public async Task Register_ValidName_ResolvesValue()
    {
        var registry = new MotdVariableRegistry();
        registry.Register("season_name", (_, _) => ValueTask.FromResult("Summer"));
        registry.Freeze();

        Assert.True(registry.Contains("season_name"));
        Assert.Equal("Summer", await registry.ResolveAsync("season_name", Context, CancellationToken.None));
    }

    [Fact]
    public void Register_DuplicateBuiltInName_RejectsReplacement()
    {
        var registry = new MotdVariableRegistry();
        registry.Register("player_name", (context, _) => ValueTask.FromResult(context.PlayerName));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("player_name", (_, _) => ValueTask.FromResult("replacement")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PlayerName")]
    [InlineData("player-name")]
    [InlineData("1player")]
    [InlineData("spéll")]
    public void Register_InvalidName_RejectsIt(string name)
    {
        var registry = new MotdVariableRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(name, (_, _) => ValueTask.FromResult("value")));
    }

    [Fact]
    public void Register_AfterFreeze_RejectsMutation()
    {
        var registry = new MotdVariableRegistry();
        registry.Freeze();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("late_name", (_, _) => ValueTask.FromResult("value")));
    }

    [Fact]
    public async Task ResolveAsync_UnknownName_RejectsIt()
    {
        var registry = new MotdVariableRegistry();
        registry.Freeze();

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await registry.ResolveAsync("missing", Context, CancellationToken.None));
    }
}
