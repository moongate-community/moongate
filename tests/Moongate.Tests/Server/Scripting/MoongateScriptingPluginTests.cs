using DryIoc;
using Moongate.Scripting;
using Moongate.Scripting.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Directories;
using SquidStd.Plugin.Abstractions.Data;

namespace Moongate.Tests.Server.Scripting;

public sealed class MoongateScriptingPluginTests
{
    [Fact]
    public void Configure_RegistersLuaNpcBrainRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-scripting-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        var container = new Container();
        container.RegisterInstance(new SquidStdOptions { AppName = "MoongateTests", AppVersion = "1.0.0" });
        container.RegisterInstance(new DirectoriesConfig(root, ["scripts"]));

        new MoongateScriptingPlugin().Configure(container, new PluginContext());

        Assert.True(container.IsRegistered<INpcBrainRuntime>());
        Assert.Equal(
            typeof(LuaNpcBrainRuntime),
            container.GetServiceRegistrations()
                .Single(registration => registration.ServiceType == typeof(INpcBrainRuntime))
                .ImplementationType
        );
    }
}
