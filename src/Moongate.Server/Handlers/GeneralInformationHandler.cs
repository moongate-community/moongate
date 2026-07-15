using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Network;
using Serilog;
using SquidStd.Network.Spans;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles general information (0xBF): dispatches on the sub-command. Screen size (0x05) and client
/// language (0x0B) are recorded on the session; the remaining benign login-time sub-commands are
/// consumed silently, and anything unknown is warned once per sub-command instead of flooding the log.
/// Sub-commands backing systems that do not exist yet (party, tooltips, spells, ...) are deliberately
/// left unhandled until those systems land.
/// </summary>
public sealed class GeneralInformationHandler : IPacketHandler<GeneralInformationPacket>, IPacketHandlerRegistration
{
    private readonly ILogger _logger = Log.ForContext<GeneralInformationHandler>();
    private readonly Lock _warnSync = new();
    private readonly HashSet<ushort> _warnedSubCommands = [];

    public void Handle(GeneralInformationPacket packet, in PacketContext context)
    {
        switch ((GeneralInformationSubCommandType)packet.SubCommand)
        {
            case GeneralInformationSubCommandType.ScreenSize:
                HandleScreenSize(packet, context.Session);
                break;

            case GeneralInformationSubCommandType.ClientLanguage:
                HandleLanguage(packet, context.Session);
                break;

            case GeneralInformationSubCommandType.WrestlingStun:
            case GeneralInformationSubCommandType.CloseStatusGump:
            case GeneralInformationSubCommandType.LoginNotice:
            case GeneralInformationSubCommandType.ClientType:
                break; // known, benign: consume without acting

            default:
                WarnOnce(packet.SubCommand);
                break;
        }
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);

    private static void HandleScreenSize(GeneralInformationPacket packet, PlayerSession session)
    {
        if (ParseScreenSize(packet.Payload) is { } size)
        {
            session.SetScreenSize(size.Width, size.Height);
        }
    }

    private static void HandleLanguage(GeneralInformationPacket packet, PlayerSession session)
    {
        if (ParseLanguage(packet.Payload) is { } language)
        {
            session.SetLanguage(language);
        }
    }

    /// <summary>
    /// Parses the screen-size (0x05) payload, whose layout after the sub-command is 2 bytes
    /// unknown/flag, then ushort width and ushort height. Returns null if the payload is too short.
    /// </summary>
    public static (int Width, int Height)? ParseScreenSize(byte[] payload)
    {
        if (payload.Length < 6)
        {
            return null;
        }

        var reader = new SpanReader(payload);
        reader.ReadUInt16(); // unknown / flag

        return (reader.ReadUInt16(), reader.ReadUInt16());
    }

    /// <summary>
    /// Parses the client-language (0x0B) payload: a 3-char ASCII code (e.g. "ENU").
    /// Returns null if the payload is empty.
    /// </summary>
    public static string? ParseLanguage(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return null;
        }

        var reader = new SpanReader(payload);

        return reader.ReadAscii(Math.Min(3, payload.Length)).TrimEnd('\0');
    }

    private void WarnOnce(ushort subCommand)
    {
        lock (_warnSync)
        {
            if (!_warnedSubCommands.Add(subCommand))
            {
                return;
            }
        }

        _logger.Warning("Unhandled 0xBF sub-command 0x{SubCommand:X4} - not implemented yet", subCommand);
    }
}
