using DryIoc;
using Moongate.Scripting.AI;
using Moongate.Scripting.Items;
using Moongate.Scripting.Modules;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Items;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;
using SquidStd.Scripting.Lua.Extensions.Scripts;
using SquidStd.Scripting.Lua.Interfaces.Events;
using SquidStd.Scripting.Lua.Interfaces.Scripts;
using SquidStd.Scripting.Lua.Services;

namespace Moongate.Scripting;

public class MoongateScriptingPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.scripting.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateScriptingPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Scripting",
            Description = "Moongate scripting plugin"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        var appConfig = container.Resolve<SquidStdOptions>();
        var directoryConfig = container.Resolve<DirectoriesConfig>();

        container.RegisterLuaEngine(
            new(
                directoryConfig.GetPath("scripts"),
                directoryConfig.GetPath("scripts"),
                appConfig.AppName,
                appConfig.AppVersion
            )
        );

        container.RegisterStdService<INpcBrainRuntime, LuaNpcBrainRuntime>();

        // The MoonSharp Script itself is not in the container: the Lua engine owns it. Resolve the
        // engine and unwrap it here, so the cast lives in one place instead of in every consumer.
        container.RegisterDelegate<IItemScriptRuntime>(
            resolver => new LuaItemScriptRuntime(
                resolver.Resolve<IScriptEngineService>() is LuaScriptEngineService lua
                    ? lua.LuaScript
                    : throw new InvalidOperationException(
                          "LuaItemScriptRuntime requires the SquidStd Lua engine implementation."
                      ),
                resolver.Resolve<DirectoriesConfig>()
            ),
            Reuse.Singleton
        );

        container.Register<ILuaInvokeMarshaller, LoopAffineInvokeMarshaller>(Reuse.Singleton);

        container.RegisterLuaEvents();

        container.RegisterScriptModule<LoggerModule>();
        container.RegisterScriptModule<GameLoopModule>();
    }
}
