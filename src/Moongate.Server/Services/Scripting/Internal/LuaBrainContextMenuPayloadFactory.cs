using Moongate.Server.Data.Internal.Scripting;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Builds Lua-friendly payload dictionaries for context menu hooks.
/// </summary>
internal static class LuaBrainContextMenuPayloadFactory
{
    public static Dictionary<string, object?> Build(LuaBrainContextMenuPayload payload)
        => new()
        {
            ["target_mobile_id"] = (uint)payload.TargetMobileId,
            ["session_id"] = payload.SessionId,
            ["menu_key"] = payload.MenuKey,
            ["requester"] = payload.Requester is null
                                ? null
                                : new Dictionary<string, object?>
                                {
                                    ["mobile_id"] = (uint)payload.Requester.Id,
                                    ["name"] = payload.Requester.Name,
                                    ["map_id"] = payload.Requester.MapId,
                                    ["location"] = new Dictionary<string, int>
                                    {
                                        ["x"] = payload.Requester.Location.X,
                                        ["y"] = payload.Requester.Location.Y,
                                        ["z"] = payload.Requester.Location.Z
                                    }
                                }
        };
}
