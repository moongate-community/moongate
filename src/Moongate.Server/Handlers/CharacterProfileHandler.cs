using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.RequestCharProfilePacket)]
public sealed class CharacterProfileHandler : BasePacketListener
{
    private const int DisplayRange = 12;
    private const string LockedProfileMessage = "Your profile is locked. You may not change it.";

    private readonly IAccountService _accountService;
    private readonly IMobileService _mobileService;

    public CharacterProfileHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IAccountService accountService,
        IMobileService mobileService
    )
        : base(outgoingPacketQueue)
    {
        _accountService = accountService;
        _mobileService = mobileService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not RequestCharProfilePacket requestPacket || session.Character is null)
        {
            return true;
        }

        var target = await ResolveTargetAsync(session, requestPacket.TargetId);

        if (target is null || !target.IsPlayer)
        {
            return true;
        }

        return requestPacket.Mode switch
        {
            0x00 => await HandleDisplayRequestAsync(session, target),
            0x01 => await HandleUpdateRequestAsync(session, target, requestPacket.ProfileText),
            _ => true
        };
    }

    private async Task<bool> HandleDisplayRequestAsync(GameSession session, UOMobileEntity target)
    {
        var requester = session.Character!;

        if (!CanDisplayProfile(requester, target))
        {
            return true;
        }

        var header = CharacterProfileHelper.BuildHeader(requester, target);
        var body = CharacterProfileHelper.GetBody(target);
        var account = requester.Id == target.Id ? await _accountService.GetAccountAsync(session.AccountId) : null;
        var footer = CharacterProfileHelper.BuildFooter(session, requester, target, account);
        var serial = CharacterProfileHelper.GetDisplaySerial(requester, target);

        Enqueue(session, new DisplayCharacterProfilePacket(serial, header, footer, body));

        return true;
    }

    private async Task<bool> HandleUpdateRequestAsync(GameSession session, UOMobileEntity target, string? profileText)
    {
        _ = target;

        var requester = session.Character!;

        if (CharacterProfileHelper.IsLocked(requester))
        {
            Enqueue(session, SpeechMessageFactory.CreateSystem(LockedProfileMessage));

            return true;
        }

        CharacterProfileHelper.SetBody(requester, profileText);
        await _mobileService.CreateOrUpdateAsync(requester);

        return true;
    }

    private async Task<UOMobileEntity?> ResolveTargetAsync(GameSession session, Moongate.UO.Data.Ids.Serial targetId)
    {
        if (session.Character is not null && session.Character.Id == targetId)
        {
            return session.Character;
        }

        return await _mobileService.GetAsync(targetId);
    }

    private static bool CanDisplayProfile(UOMobileEntity requester, UOMobileEntity target)
    {
        if (requester.MapId != target.MapId)
        {
            return false;
        }

        if (!requester.Location.InRange(target.Location, DisplayRange))
        {
            return false;
        }

        return requester.Id == target.Id || !target.IsHidden;
    }
}
