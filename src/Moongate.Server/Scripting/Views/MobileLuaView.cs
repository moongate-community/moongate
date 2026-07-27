using Moongate.Persistence.Entities;
using SquidStd.Scripting.Lua.Interfaces.Scripts;

namespace Moongate.Server.Scripting.Views;

/// <summary>Projects a <see cref="MobileEntity" /> into the Lua field table returned by <c>mobile.get</c>.</summary>
public sealed record MobileLuaView(MobileEntity Mobile) : ILuaTable
{
    public Dictionary<string, object?> ToDictionary()
        => new()
        {
            ["id"] = Mobile.Id.Value,
            ["name"] = Mobile.Name,
            ["map"] = Mobile.MapId,
            ["x"] = Mobile.Position.X,
            ["y"] = Mobile.Position.Y,
            ["z"] = Mobile.Position.Z,
            ["direction"] = Mobile.Direction.ToString(),
            ["gender"] = Mobile.Gender.ToString(),
            ["race"] = Mobile.Race.ToString(),
            ["profession"] = (int)Mobile.ProfessionId,
            ["str"] = Mobile.Strength,
            ["dex"] = Mobile.Dexterity,
            ["int"] = Mobile.Intelligence,
            ["backpack"] = Mobile.BackpackId.Value
        };
}
