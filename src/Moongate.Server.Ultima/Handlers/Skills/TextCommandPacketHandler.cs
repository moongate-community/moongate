using System.Globalization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Skills;

/// <summary>
///     Acts on the text commands of the client (0x12). One that uses a skill (kind 0x24) carries the number of the
///     skill at the start of its text, as "21 0"; the other kinds are recognised and not handled yet.
/// </summary>
public sealed class TextCommandPacketHandler : IPacketHandler<TextCommandPacket>
{
    private const int LoggedTextLength = 32;

    private readonly ILogger _logger = Log.ForContext<TextCommandPacketHandler>();
    private readonly ISkillUseService _skills;

    public TextCommandPacketHandler(ISkillUseService skills)
    {
        _skills = skills;
    }

    public void Handle(GameSession session, TextCommandPacket packet)
    {
        if (packet.Kind != TextCommandPacket.UseSkill)
        {
            _logger.Debug(
                "Received text command 0x{Kind:X2} from session {SessionId}: not handled yet",
                packet.Kind,
                session.SessionId
            );

            return;
        }

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
                packet.Text.Length > LoggedTextLength ? packet.Text[..LoggedTextLength] : packet.Text
            );

            return;
        }

        _skills.Use(session, (SkillType)skill);
    }
}
