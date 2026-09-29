using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Speech;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Speech;

/// <summary>
///     Creates player and system text messages and sends them to a game session.
/// </summary>
public static class SpeechMessageHelper
{
    public static UnicodeSpeechMessagePacket CreatePlayer(
        Serial serial,
        ushort body,
        string name,
        SpeechRequestData speech
    )
    {
        ArgumentNullException.ThrowIfNull(speech);

        return new(serial, body, speech.Type, speech.Hue, speech.Font, speech.Language, name, speech.Text);
    }

    public static UnicodeSpeechMessagePacket CreateSystem(string text, Hue hue)
    {
        return new(
            new Serial(uint.MaxValue),
            ushort.MaxValue,
            SpeechType.System,
            hue,
            SpeechFontType.Normal,
            "ENU",
            "System",
            text
        );
    }

    public static bool TrySend(IPacketSendService sender, GameSession recipient, UnicodeSpeechMessagePacket packet)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(packet);

        var connection = recipient.NetworkSession.Client;

        return connection is { IsConnected: true } && sender.TrySend(recipient.SessionId, connection, packet);
    }
}
