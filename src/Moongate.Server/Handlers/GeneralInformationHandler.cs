using Moongate.Network.Packets.Incoming;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Server.Data;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Network;
using Moongate.UO.Data.Types;
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

            case GeneralInformationSubCommandType.ExtendedStats:
                HandleStatLockChange(packet, context.Session);
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

    // Applies the lock the client set to the attached mobile, then echoes the resulting state back:
    // the echo is what keeps the arrows honest when the requested lock had to be clamped.
    private static void HandleStatLockChange(GeneralInformationPacket packet, PlayerSession session)
    {
        if (session.Character is not { } mobile || ParseStatLockChange(packet.Payload) is not var (stat, statLock))
        {
            return;
        }

        switch (stat)
        {
            case StatType.Str:
                mobile.StrengthLock = statLock;
                break;

            case StatType.Dex:
                mobile.DexterityLock = statLock;
                break;

            case StatType.Int:
                mobile.IntelligenceLock = statLock;
                break;

            default:
                return;
        }

        session.Send(
            new StatLockInfoPacket(mobile.Id, mobile.StrengthLock, mobile.DexterityLock, mobile.IntelligenceLock)
        );
    }

    /// <summary>
    /// Parses the stat-lock change (0x1A) payload: the stat index (0 strength, 1 dexterity,
    /// 2 intelligence) and the requested lock state. A lock value the client should not have sent is
    /// clamped to <see cref="StatLockType.Up" />, as ModernUO does. Returns null when the payload is
    /// too short or names a stat that does not exist.
    /// </summary>
    public static (StatType Stat, StatLockType Lock)? ParseStatLockChange(byte[] payload)
    {
        if (payload.Length < 2)
        {
            return null;
        }

        var stat = payload[0] switch
        {
            0 => StatType.Str,
            1 => StatType.Dex,
            2 => StatType.Int,
            _ => StatType.None
        };

        if (stat == StatType.None)
        {
            return null;
        }

        var statLock = payload[1] > (byte)StatLockType.Locked ? StatLockType.Up : (StatLockType)payload[1];

        return (stat, statLock);
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
