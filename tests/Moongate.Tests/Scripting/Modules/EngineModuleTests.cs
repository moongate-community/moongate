using System.Reflection;
using Lua;
using Lua.Standard;
using Moongate.Core.Utils;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Modules;

namespace Moongate.Tests.Scripting.Modules;

public sealed class EngineModuleTests
{
    [Fact]
    public void Engine_ExposesNameVersionCodenameAndPlatform()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new EngineModule());

        var result = SyncValueTask.Run(
            state.DoStringAsync("return engine.name, engine.version, engine.codename, engine.platform", "t")
        );

        Assert.Equal("Moongate", result[0].Read<string>());
        Assert.Equal(VersionUtils.GetVersion(), result[1].Read<string>());
        Assert.Equal(
            VersionUtils.GetCodename(Assembly.GetEntryAssembly() ?? typeof(EngineModule).Assembly),
            result[2].Read<string>()
        );
        Assert.Equal(Environment.OSVersion.Platform.ToString(), result[3].Read<string>());
    }

    [Fact]
    public void Log_ExposesLevelConstantsAndFunctions()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new LogModule());

        var result = SyncValueTask.Run(
            state.DoStringAsync(
                "log.info('hello {Name}', 'x') log.warning('w') log.error('e') log.debug('d') return log.LEVEL_INFO, type(log.info)",
                "t"
            )
        );

        Assert.Equal(2, result[0].Read<double>());
        Assert.Equal("function", result[1].Read<string>());
    }
}
