using DryIoc;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Services.Motd;

namespace Moongate.Tests.Server.Ultima.Extensions;

public sealed class MotdContainerExtensionsTests
{
    [Fact]
    public async Task RegisterMotdVariable_UsesExistingSingletonRegistry()
    {
        using var container = new Container();
        container.Register<IMotdVariableRegistry, MotdVariableRegistry>(Reuse.Singleton);
        var registry = container.Resolve<IMotdVariableRegistry>();

        container.RegisterMotdVariable("season_name", (_, _) => ValueTask.FromResult("Summer"));

        Assert.Same(registry, container.Resolve<IMotdVariableRegistry>());
        Assert.Equal(
            "Summer",
            await registry.ResolveAsync("season_name", new("Moongate", "Felucca", "1", "Dawn", "Aria", 1),
                CancellationToken.None)
        );
    }
}
