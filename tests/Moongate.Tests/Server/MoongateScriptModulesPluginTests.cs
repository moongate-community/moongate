using DryIoc;
using Moongate.Server;
using Moongate.Server.Scripting;
using Moongate.UO.Data.Types;
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

    [Fact]
    public void Configure_RegistersMobileModule()
    {
        var container = new Container();

        new MoongateScriptModulesPlugin().Configure(container, new());

        var modules = container.Resolve<List<ScriptModuleData>>();
        Assert.Contains(modules, module => module.ModuleType == typeof(MobileModule));
    }

    [Fact]
    public void Configure_RegistersSkillNameEnum()
    {
        var container = new Container();

        new MoongateScriptModulesPlugin().Configure(container, new());

        var enums = container.Resolve<List<ScriptEnumData>>();
        Assert.Contains(enums, entry => entry.EnumType == typeof(SkillName));
    }
}
