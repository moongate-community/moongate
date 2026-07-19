using DryIoc;
using Moongate.Core.Types;
using Moongate.Server;
using Moongate.Server.Scripting;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Scripting.Lua.Data.Internal;

namespace Moongate.Tests.Server;

public class MoongateScriptModulesPluginTests
{
    [Fact]
    public void Configure_RegistersAccountModule()
    {
        var container = new Container();

        new MoongateScriptModulesPlugin().Configure(container, new());

        var modules = container.Resolve<List<ScriptModuleData>>();
        Assert.Contains(modules, module => module.ModuleType == typeof(AccountModule));
    }

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

    [Theory, InlineData(typeof(AccountLevelType)), InlineData(typeof(SkillName)), InlineData(typeof(GenderType)),
     InlineData(typeof(RaceType)), InlineData(typeof(LayerType))]
    public void Configure_RegistersScriptEnum(Type enumType)
    {
        var container = new Container();

        new MoongateScriptModulesPlugin().Configure(container, new());

        var enums = container.Resolve<List<ScriptEnumData>>();
        Assert.Contains(enums, entry => entry.EnumType == enumType);
    }
}
