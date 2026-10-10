using System.Buffers.Binary;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Dispatches the extended commands (0xBF) by subcommand: 0x10 asks for one object's tooltip, answered with 0xD6
///     when the character can see it; 0x1A sets the lock of one stat of the character; 0x13 asks for the context menu
///     of a mobile or an item and 0x15 chooses an entry of it; 0x1C casts the spell of one of the client's icons. The
///     others are recognised and ignored for now.
/// </summary>
public sealed class ExtendedCommandPacketHandler : IPacketHandler<ExtendedCommandPacket>
{
    private const ushort QueryPropertiesSubcommand = 0x10;
    private const ushort StatLockSubcommand = 0x1A;

    // A context menu: the client asks for the one of what was clicked, and later says which entry was chosen.
    private const ushort ContextMenuRequestSubcommand = 0x13;
    private const ushort ContextMenuSelectSubcommand = 0x15;
    private const int ContextMenuRequestLength = 4;
    private const int ContextMenuSelectLength = 6;
    private const int StatLockLength = 2;

    // The client's spell icon: whether a book follows (1) with its serial, then the number of the spell from 1.
    private const ushort CastSpellSubcommand = 0x1C;
    private const ushort WithBook = 1;

    private readonly ILogger _logger = Log.ForContext<ExtendedCommandPacketHandler>();
    private readonly ITooltipService _tooltips;
    private readonly IPacketSendService _sender;
    private readonly IMobileService? _mobiles;
    private readonly IMobileStateService? _state;
    private readonly IContextMenuService? _contextMenus;
    private readonly ISpellCastService? _casts;
    private readonly IItemService? _items;

    public ExtendedCommandPacketHandler(
        ITooltipService tooltips,
        IPacketSendService sender,
        IMobileService? mobiles = null,
        IMobileStateService? state = null,
        IContextMenuService? contextMenus = null,
        ISpellCastService? casts = null,
        IItemService? items = null
    )
    {
        _items = items;
        _casts = casts;
        _contextMenus = contextMenus;
        _mobiles = mobiles;
        _state = state;
        _tooltips = tooltips;
        _sender = sender;
    }

    public void Handle(GameSession session, ExtendedCommandPacket packet)
    {
        if (packet.Subcommand == QueryPropertiesSubcommand && packet.Payload.Length >= 4)
        {
            var serial = new Serial(BinaryPrimitives.ReadUInt32BigEndian(packet.Payload));

            if (_tooltips.TryBuildFor(session.CharacterId, serial, out var list, session.AccountType))
            {
                _sender.TrySend(session.SessionId, new PropertyListPacket(serial, list));
            }

            return;
        }

        if (packet.Subcommand == StatLockSubcommand && packet.Payload.Length >= StatLockLength)
        {
            SetStatLock(session, packet.Payload[0], packet.Payload[1]);

            return;
        }

        if (packet.Subcommand == ContextMenuRequestSubcommand && packet.Payload.Length >= ContextMenuRequestLength)
        {
            _contextMenus?.Request(session, new Serial(BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)));

            return;
        }

        if (packet.Subcommand == ContextMenuSelectSubcommand && packet.Payload.Length >= ContextMenuSelectLength)
        {
            _contextMenus?.Select(
                session,
                new Serial(BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)),
                BinaryPrimitives.ReadUInt16BigEndian(packet.Payload.AsSpan(sizeof(uint)))
            );

            return;
        }

        if (packet.Subcommand == CastSpellSubcommand)
        {
            CastSpell(session, packet.Payload);

            return;
        }

        _logger.Debug(
            "Session {SessionId} sent extended command 0x{Subcommand:X2}, not handled yet",
            session.SessionId,
            packet.Subcommand
        );
    }

    private void CastSpell(GameSession session, byte[] payload)
    {
        if (_casts is null ||
            _mobiles is null ||
            payload.Length < sizeof(ushort) ||
            !session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var caster) ||
            !_mobiles.IsInWorld(caster.Id))
        {
            return;
        }

        var offset = sizeof(ushort);
        ItemEntity? book = null;

        if (BinaryPrimitives.ReadUInt16BigEndian(payload) == WithBook)
        {
            if (payload.Length < offset + sizeof(uint))
            {
                return;
            }

            _items?.TryGet(new Serial(BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(offset))), out book);
            offset += sizeof(uint);
        }

        if (payload.Length < offset + sizeof(ushort))
        {
            return;
        }

        _casts.CastFromBook(caster, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset)), book);
    }

    // The status window's lock arrows: the stat is 0 strength, 1 dexterity, 2 intelligence, the lock as the client
    // counts it; as ModernUO, a lock past locked reads as up.
    private void SetStatLock(GameSession session, byte stat, byte lockValue)
    {
        if (_mobiles is null ||
            _state is null ||
            !session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var character) ||
            !_mobiles.IsInWorld(character.Id))
        {
            return;
        }

        _state.SetStatLock(
            character,
            (StatType)stat,
            lockValue > (byte)StatLockType.Locked ? StatLockType.Up : (StatLockType)lockValue
        );
    }
}
