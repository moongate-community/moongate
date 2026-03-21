using System.Buffers.Binary;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Events.Party;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Handlers;

[RegisterPacketHandler(PacketDefinition.GeneralInformationPacket)]
public class GeneralInformationHandler : BasePacketListener
{
    private readonly IGameEventBusService _gameEventBusService;

    public GeneralInformationHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameEventBusService gameEventBusService
    )
        : base(outgoingPacketQueue)
    {
        _gameEventBusService = gameEventBusService;
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not GeneralInformationPacket generalInformationPacket)
        {
            return true;
        }

        var payload = generalInformationPacket.SubcommandData.Span;

        switch (generalInformationPacket.SubcommandType)
        {
            case GeneralInformationSubcommandType.PartySystem:
                await HandlePartySystemAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.Action3DClient:
                await HandleAction3DClientAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.ClientType:
                HandleClientType(session, payload);

                break;
            case GeneralInformationSubcommandType.StatLockChange:
                await HandleStatLockChangeAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.RequestPopupMenu:
                await HandleRequestPopupMenuAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.PopupEntrySelection:
                await HandlePopupEntrySelectionAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.UseTargetedItem:
                await HandleUseTargetedItemAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.CastTargetedSpell:
                await HandleCastTargetedSpellAsync(session, payload);

                break;
            case GeneralInformationSubcommandType.UseTargetedSkill:
                await HandleUseTargetedSkillAsync(session, payload);

                break;
        }

        return true;
    }

    private static void HandleClientType(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
        {
            return;
        }

        var rawClientType = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var clientType = rawClientType switch
        {
            0x02u => ClientType.KR,
            0x03u => ClientType.SA,
            _ => ClientType.Classic
        };

        session.NetworkSession.SetClientType(clientType);
    }

    private ValueTask HandleAction3DClientAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (session.Character is null)
        {
            return ValueTask.CompletedTask;
        }

        if (
            !AnimationUtils.TryReadClientAction3D(payload, out var action) ||
            !AnimationUtils.IsValidClientAction3DAnimation(action)
        )
        {
            return ValueTask.CompletedTask;
        }

        return _gameEventBusService.PublishAsync(
            new MobilePlayAnimationEvent(
                session.Character.Id,
                session.Character.MapId,
                session.Character.Location,
                AnimationUtils.ClampActionToPacket(action)
            )
        );
    }

    private ValueTask HandleCastTargetedSpellAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 6)
        {
            return ValueTask.CompletedTask;
        }

        var spellId = BinaryPrimitives.ReadUInt16BigEndian(payload);
        var targetSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload[2..]);

        return _gameEventBusService.PublishAsync(new TargetedSpellCastEvent(session.SessionId, spellId, targetSerial));
    }

    private ValueTask HandlePartySystemAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        var subcommand = payload[0];
        var subcommandPayload = payload.Length == 1 ? [] : payload[1..].ToArray();

        return _gameEventBusService.PublishAsync(
            new PartySystemCommandEvent(session.SessionId, subcommand, subcommandPayload)
        );
    }

    private ValueTask HandlePopupEntrySelectionAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (!SupportsContextMenu(session))
        {
            return ValueTask.CompletedTask;
        }

        if (payload.Length != 6)
        {
            return ValueTask.CompletedTask;
        }

        var targetSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload);
        var entryTag = BinaryPrimitives.ReadUInt16BigEndian(payload[4..]);

        return _gameEventBusService.PublishAsync(
            new ContextMenuEntrySelectedEvent(session.SessionId, targetSerial, entryTag)
        );
    }

    private ValueTask HandleRequestPopupMenuAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (!SupportsContextMenu(session))
        {
            return ValueTask.CompletedTask;
        }

        if (payload.Length != 4)
        {
            return ValueTask.CompletedTask;
        }

        var targetSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload);

        return _gameEventBusService.PublishAsync(new ContextMenuRequestedEvent(session.SessionId, targetSerial));
    }

    private ValueTask HandleStatLockChangeAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 2)
        {
            return ValueTask.CompletedTask;
        }

        var statIndex = payload[0];
        var lockState = payload[1];

        if (statIndex > 2 || lockState > 2)
        {
            return ValueTask.CompletedTask;
        }

        return _gameEventBusService.PublishAsync(
            new StatLockChangeRequestedEvent(session.SessionId, (Stat)statIndex, (UOSkillLock)lockState)
        );
    }

    private ValueTask HandleUseTargetedItemAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 8)
        {
            return ValueTask.CompletedTask;
        }

        var itemSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload);
        var targetSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload[4..]);

        return _gameEventBusService.PublishAsync(new TargetedItemUseEvent(session.SessionId, itemSerial, targetSerial));
    }

    private ValueTask HandleUseTargetedSkillAsync(GameSession session, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 6)
        {
            return ValueTask.CompletedTask;
        }

        var skillId = BinaryPrimitives.ReadUInt16BigEndian(payload);
        var targetSerial = (Serial)BinaryPrimitives.ReadUInt32BigEndian(payload[2..]);

        return _gameEventBusService.PublishAsync(new TargetedSkillUseEvent(session.SessionId, skillId, targetSerial));
    }

    private static bool SupportsContextMenu(GameSession session)
    {
        if (session.ClientVersion is null)
        {
            return false;
        }

        return session.ClientVersion.ProtocolChanges.HasFlag(ProtocolChanges.StygianAbyss);
    }
}
