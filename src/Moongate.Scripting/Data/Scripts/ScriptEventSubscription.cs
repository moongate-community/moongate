using Lua;

namespace Moongate.Scripting.Data.Scripts;

/// <summary>
///     One Lua function subscribed to a script event, and the file that subscribed it.
/// </summary>
internal sealed record ScriptEventSubscription(string Handle, string Owner, LuaFunction Function);
