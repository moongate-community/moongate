using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Gumps;
using Moongate.Server.Scripting;
using SquidStd.Scripting.Lua.Services;
using SquidStd.Scripting.Lua.Interfaces.Scripts;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using Moongate.Server.Scripting.Refs;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;
using SquidStd.Scripting.Lua.Extensions.Scripts;

namespace Moongate.Server.Plugins;

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
        // Same unwrap as MoongateScriptingPlugin does for the item-script runtime: the MoonSharp
        // Script belongs to the Lua engine, not the container.
        container.RegisterDelegate(
            resolver => new MobileRefFactory(
                resolver.Resolve<IScriptEngineService>() is LuaScriptEngineService lua
                    ? lua.LuaScript
                    : throw new InvalidOperationException(
                        "MobileRefFactory requires the SquidStd Lua engine implementation."
                    ),
                resolver.Resolve<IPersistenceService>(),
                resolver.Resolve<IChatService>(),
                resolver.Resolve<IMobileService>(),
                resolver.Resolve<IItemService>()
            ),
            Reuse.Singleton
        );

        container.RegisterDelegate(
            resolver => new GumpBuilderFactory(
                resolver.Resolve<IScriptEngineService>() is LuaScriptEngineService gumpLua
                    ? gumpLua.LuaScript
                    : throw new InvalidOperationException(
                        "GumpBuilderFactory requires the SquidStd Lua engine implementation."
                    )
            ),
            Reuse.Singleton
        );

        container.RegisterScriptModule<AccountModule>();
        container.RegisterScriptModule<ItemModule>();
        container.RegisterScriptModule<MobileModule>();
        container.RegisterScriptModule<LootModule>();
        container.RegisterScriptModule<ChatModule>();
        container.RegisterScriptModule<AiModule>();
        container.RegisterScriptModule<MemoryModule>();
        container.RegisterScriptModule<GumpModule>();

        container.RegisterScriptEnum<AccountLevelType>();
        container.RegisterScriptEnum<SkillName>();
        container.RegisterScriptEnum<GenderType>();
        container.RegisterScriptEnum<RaceType>();
        container.RegisterScriptEnum<LayerType>();
    }
}
