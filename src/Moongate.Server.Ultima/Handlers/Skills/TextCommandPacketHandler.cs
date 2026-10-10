using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Skills;

/// <summary>
///     Acts on the text commands of the client (0x12). One that uses a skill (kind 0x24) carries the number of the
///     skill at the start of its text, as "21 0"; one that casts a spell (0x27 from the spellbook, 0x56 from a macro) the
///     number of the spell, the book's serial after it for the first; one that opens the spellbook (0x43) its kind. The
///     other kinds are recognised and not handled yet.
/// </summary>
public sealed class TextCommandPacketHandler : IPacketHandler<TextCommandPacket>
{
    private const int LoggedTextLength = 32;
    private const int MagerySpellbook = 1;

    private readonly ILogger _logger = Log.ForContext<TextCommandPacketHandler>();
    private readonly ISkillUseService _skills;
    private readonly ISpellCastService? _casts;
    private readonly ISpellbookService? _books;
    private readonly IMobileService? _mobiles;
    private readonly IItemService? _items;

    public TextCommandPacketHandler(
        ISkillUseService skills,
        ISpellCastService? casts = null,
        ISpellbookService? books = null,
        IMobileService? mobiles = null,
        IItemService? items = null
    )
    {
        _items = items;
        _mobiles = mobiles;
        _books = books;
        _casts = casts;
        _skills = skills;
    }

    public void Handle(GameSession session, TextCommandPacket packet)
    {
        switch (packet.Kind)
        {
            case TextCommandPacket.UseSkill:
                UseSkill(session, packet);

                return;
            case TextCommandPacket.CastFromBook:
            case TextCommandPacket.CastFromMacro:
                CastSpell(session, packet);

                return;
            case TextCommandPacket.OpenSpellbook:
                OpenSpellbook(session, packet);

                return;
            default:
                _logger.Debug(
                    "Received text command 0x{Kind:X2} from session {SessionId}: not handled yet",
                    packet.Kind,
                    session.SessionId
                );

                return;
        }
    }

    private void UseSkill(GameSession session, TextCommandPacket packet)
    {
        var text = packet.Text.AsSpan();
        var digits = text.IndexOfAnyExceptInRange('0', '9');
        var number = digits < 0 ? text : text[..digits];

        if (!byte.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var skill) ||
            !Enum.IsDefined((SkillType)skill))
        {
            // The text is the client's, as long as a packet can be: the log takes its start.
            _logger.Debug(
                "Session {SessionId} asked to use the skill {Text}, which is none",
                session.SessionId,
                Logged(packet.Text)
            );

            return;
        }

        _skills.Use(session, (SkillType)skill);
    }

    // "5" or "5 1073741825": the number of the spell as the spellbook counts it from 1, then the serial of the book.
    private void CastSpell(GameSession session, TextCommandPacket packet)
    {
        if (_casts is null || _mobiles is null || !TryCharacter(session, out var caster))
        {
            return;
        }

        var parts = packet.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var spell))
        {
            _logger.Debug(
                "Session {SessionId} asked to cast {Text}, which is no spell number",
                session.SessionId,
                Logged(packet.Text)
            );

            return;
        }

        _casts.CastFromBook(caster, spell, FindBook(packet.Kind, parts));
    }

    // The book a request from the spellbook names; null for a macro, which names none.
    private ItemEntity? FindBook(byte kind, string[] parts)
    {
        return kind == TextCommandPacket.CastFromBook &&
               parts.Length > 1 &&
               TryNumber(parts[1], out var serial) &&
               _items is not null &&
               _items.TryGet(new Serial(serial), out var found)
            ? found
            : null;
    }

    private void OpenSpellbook(GameSession session, TextCommandPacket packet)
    {
        if (_books is null || _mobiles is null || !TryCharacter(session, out var player))
        {
            return;
        }

        // The kind of book: only the book of Magery is here, and a text that is none is it as well.
        if (int.TryParse(packet.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var kind) && kind != MagerySpellbook)
        {
            return;
        }

        if (_books.FindCarried(player, 0) is { } book)
        {
            _books.Open(session, book);
        }
    }

    private bool TryCharacter(GameSession session, out MobileEntity character)
    {
        // Safe: out parameter; callers read it only when the method returns true.
        character = null!;

        return session.CharacterId.IsValid &&
               _mobiles!.TryGet(session.CharacterId, out character!) &&
               _mobiles.IsInWorld(character.Id);
    }

    private static bool TryNumber(string text, out uint value)
    {
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
            : uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string Logged(string text)
    {
        return text.Length > LoggedTextLength ? text[..LoggedTextLength] : text;
    }
}
