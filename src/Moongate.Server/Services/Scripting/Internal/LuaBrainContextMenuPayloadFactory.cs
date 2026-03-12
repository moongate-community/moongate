using Moongate.Server.Data.Internal.Scripting;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Builds pooled Lua-friendly payload dictionaries for context menu hooks.
/// </summary>
internal static class LuaBrainContextMenuPayloadFactory
{
    public static LuaBrainContextMenuPayloadLease Rent(LuaBrainContextMenuPayload payload)
    {
        var root = LuaBrainDictionaryPool.RentObjectDictionary();
        root["target_mobile_id"] = (uint)payload.TargetMobileId;
        root["session_id"] = payload.SessionId;
        root["menu_key"] = payload.MenuKey;

        Dictionary<string, object?>? requester = null;
        Dictionary<string, int>? requesterLocation = null;

        if (payload.Requester is not null)
        {
            requester = LuaBrainDictionaryPool.RentObjectDictionary();
            requesterLocation = LuaBrainDictionaryPool.RentIntDictionary();
            requesterLocation["x"] = payload.Requester.Location.X;
            requesterLocation["y"] = payload.Requester.Location.Y;
            requesterLocation["z"] = payload.Requester.Location.Z;

            requester["mobile_id"] = (uint)payload.Requester.Id;
            requester["name"] = payload.Requester.Name;
            requester["map_id"] = payload.Requester.MapId;
            requester["location"] = requesterLocation;
            root["requester"] = requester;
        }
        else
        {
            root["requester"] = null;
        }

        return new LuaBrainContextMenuPayloadLease(root, requester, requesterLocation);
    }
}

internal readonly struct LuaBrainContextMenuPayloadLease : IDisposable
{
    private readonly Dictionary<string, object?> _root;
    private readonly Dictionary<string, object?>? _requester;
    private readonly Dictionary<string, int>? _requesterLocation;

    public LuaBrainContextMenuPayloadLease(
        Dictionary<string, object?> root,
        Dictionary<string, object?>? requester,
        Dictionary<string, int>? requesterLocation
    )
    {
        _root = root;
        _requester = requester;
        _requesterLocation = requesterLocation;
    }

    public Dictionary<string, object?> Payload => _root;

    public void Dispose()
    {
        if (_requesterLocation is not null)
        {
            LuaBrainDictionaryPool.Return(_requesterLocation);
        }

        if (_requester is not null)
        {
            LuaBrainDictionaryPool.Return(_requester);
        }

        LuaBrainDictionaryPool.Return(_root);
    }
}
