using DryIoc;
using Moongate.Scripting.Modules;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;
using SquidStd.Scripting.Lua.Data.Config;
using SquidStd.Scripting.Lua.Extensions.Scripts;

namespace Moongate.Scripting;

public class MoongateScriptingPlugin : ISquidStdPlugin
{
    public void Configure(IContainer container, PluginContext context)
    {
        var appConfig = container.Resolve<SquidStdOptions>();
        var directoryConfig = container.Resolve<DirectoriesConfig>();

        container.RegisterLuaEngine(
            new LuaEngineConfig(
                directoryConfig.GetPath("scripts"),
                directoryConfig.GetPath("scripts"),
                appConfig.AppName,
                appConfig.AppVersion
            )
        );

        container.RegisterLuaEvents();

        container.RegisterScriptModule<LoggerModule>();
        container.RegisterScriptModule<GameLoopModule>();
    }

    public PluginMetadata Metadata
        => new PluginMetadata()
        {
            Id = "moongate.scripting.plugin",
            Version = new Version(VersionUtils.GetVersion(typeof(MoongateScriptingPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Scripting",
            Description = "Moongate scripting plugin",
        };
}
