using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Modules;

[ScriptModule("resurrection", "Provides resurrection offer helpers for Lua gumps and item scripts.")]
public sealed class ResurrectionModule
{
    private const int AnkhRange = 2;
    private const string AnkhResurrectionSource = "ankh";

    private readonly IResurrectionOfferService _resurrectionOfferService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IItemService _itemService;

    public ResurrectionModule(
        IResurrectionOfferService resurrectionOfferService,
        IGameNetworkSessionService gameNetworkSessionService,
        IItemService itemService
    )
    {
        _resurrectionOfferService = resurrectionOfferService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _itemService = itemService;
    }

    [ScriptFunction("offer_ankh", "Creates a resurrection offer from an ankh item for the current ghost player.")]
    public bool OfferAnkh(long sessionId, uint characterId, uint itemSerial)
    {
        if (
            sessionId <= 0 ||
            characterId == 0 ||
            itemSerial == 0 ||
            !_gameNetworkSessionService.TryGet(sessionId, out var session) ||
            session.CharacterId != (Serial)characterId ||
            session.Character is null ||
            session.Character.IsAlive
        )
        {
            return false;
        }

        var item = _itemService.GetItemAsync((Serial)itemSerial).GetAwaiter().GetResult();

        if (
            item is null ||
            item.MapId != session.Character.MapId ||
            session.Character.Location.GetDistance(item.Location) > AnkhRange ||
            !item.TryGetCustomString(ItemCustomParamKeys.Interaction.ResurrectionSource, out var source) ||
            !string.Equals(source, AnkhResurrectionSource, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return _resurrectionOfferService
               .TryCreateOfferAsync(
                   sessionId,
                   session.Character.Id,
                   ResurrectionOfferSourceType.Ankh,
                   item.Id,
                   item.MapId,
                   item.Location
               )
               .GetAwaiter()
               .GetResult();
    }

    [ScriptFunction("accept", "Accepts the pending resurrection offer for a session.")]
    public bool Accept(long sessionId)
    {
        if (sessionId <= 0)
        {
            return false;
        }

        return _resurrectionOfferService.TryAcceptAsync(sessionId).GetAwaiter().GetResult();
    }

    [ScriptFunction("decline", "Declines the pending resurrection offer for a session.")]
    public bool Decline(long sessionId)
    {
        if (sessionId <= 0)
        {
            return false;
        }

        _resurrectionOfferService.Decline(sessionId);

        return true;
    }
}
