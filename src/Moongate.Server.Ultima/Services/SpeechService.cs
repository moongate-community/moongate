using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Overhead speech and sounds of a mobile, sent to the players within the say range as the player speech handler does.
/// </summary>
public sealed class SpeechService : ISpeechService
{
    public const int SayRange = 15;

    private static readonly Hue SpeechHue = new(0x03B2);

    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public SpeechService(ISessionService sessions, IMobileService mobiles, IPacketSendService sender)
    {
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
    }

    public int Say(MobileEntity speaker, string text)
    {
        var message = new UnicodeSpeechMessagePacket(
            speaker.Id,
            (ushort)Math.Clamp(speaker.Body, 0, ushort.MaxValue),
            SpeechType.Regular,
            SpeechHue,
            SpeechFontType.Normal,
            "ENU",
            speaker.Name,
            text
        );
        return SendAround(speaker, message);
    }

    public int PlaySound(MobileEntity source, int sound)
    {
        return SendAround(source, new PlaySoundPacket(sound, source.Location));
    }

    private int SendAround(MobileEntity source, IOutgoingPacket packet)
    {
        var sent = 0;

        foreach (var session in _sessions.GetAll())
        {
            if (session.CharacterId.IsValid &&
                _mobiles.TryGet(session.CharacterId, out var listener) &&
                listener.Map == source.Map &&
                listener.Location.InRange(source.Location, SayRange) &&
                session.NetworkSession.Client is { IsConnected: true } connection &&
                _sender.TrySend(session.SessionId, connection, packet))
            {
                sent++;
            }
        }

        return sent;
    }
}