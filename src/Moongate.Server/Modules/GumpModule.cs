using Moongate.Server.Modules.Builders;
using Moongate.Scripting.Attributes.Scripts;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("gump", "Provides fluent gump layout building APIs.")]
/// <summary>
/// Exposes gump-building helpers to Lua scripts.
/// </summary>
public sealed class GumpModule
{
    private static bool _isBuilderTypeRegistered;

    [ScriptFunction("create", "Creates a new gump builder instance.")]
    public LuaGumpBuilder Create()
    {
        if (!_isBuilderTypeRegistered)
        {
            UserData.RegisterType<LuaGumpBuilder>();
            _isBuilderTypeRegistered = true;
        }

        return new();
    }
}
