using DryIoc;
using Moongate.Server;
using Moongate.Server.Scripting;
using SquidStd.Scripting.Lua.Data.Internal;

namespace Moongate.Tests.Server;

public class MoongateScriptModulesPluginTests
{
    [Fact]
    public void Configure_RegistersItemModule()
    {
        var container = new Container();

        new MoongateScriptModulesPlugin().Configure(container, new());

        var modules = container.Resolve<List<ScriptModuleData>>();
        Assert.Contains(modules, module => module.ModuleType == typeof(ItemModule));
    }
}
