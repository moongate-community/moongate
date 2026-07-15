using DryIoc;
using Moongate.Server.Scripting;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;
using SquidStd.Scripting.Lua.Extensions.Scripts;

namespace Moongate.Server;

/// <summary>Registers Moongate's Server-side Lua script modules, which depend on Server services.</summary>
public class MoongateScriptModulesPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.scriptmodules.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateScriptModulesPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Script Modules",
            Description = "Server-side Lua script modules"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterScriptModule<ItemModule>();
        container.RegisterScriptModule<MobileModule>();
        container.RegisterScriptModule<LootModule>();

        container.RegisterScriptEnum<SkillName>();
        container.RegisterScriptEnum<GenderType>();
        container.RegisterScriptEnum<RaceType>();
        container.RegisterScriptEnum<LayerType>();
    }
}
