using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Speech;
using Moongate.Ultima.Types;

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

        return SendAround(speaker.Map, speaker.Location, message, speaker);
    }

    public int SayCliloc(MobileEntity speaker, int cliloc, string arguments = "", string affix = "")
    {
        var body = Math.Clamp(speaker.Body, 0, ushort.MaxValue);

        // A text with something of the server after it travels in a packet of its own.
        IOutgoingPacket message = affix.Length == 0
            ? LocalizedMessagePacket.Spoken(speaker.Id, body, cliloc, speaker.Name, arguments)
            : LocalizedMessageAffixPacket.Spoken(speaker.Id, body, cliloc, speaker.Name, affix, arguments);

        return SendAround(speaker.Map, speaker.Location, message, speaker);
    }

    public int PlaySound(MobileEntity source, int sound)
    {
        return SendAround(source.Map, source.Location, new PlaySoundPacket(sound, source.Location), source);
    }

    public int PlaySound(MapType map, Point3D location, int sound)
    {
        return SendAround(map, location, new PlaySoundPacket(sound, location));
    }

    public bool Tell(MobileEntity player, string text, int? hue = null)
    {
        return _sessions.TryGetByCharacterId(player.Id, out var session) &&
               SpeechMessageHelper.TrySend(
                   _sender,
                   session,
                   SpeechMessageHelper.CreateSystem(text, hue is { } colour ? new((ushort)colour) : SpeechHue)
               );
    }

    public bool TellCliloc(MobileEntity player, int cliloc, string arguments = "", int? hue = null)
    {
        return _sessions.TryGetByCharacterId(player.Id, out var session) &&
               _sender.TrySend(session.SessionId, LocalizedMessagePacket.System(cliloc, arguments, hue));
    }

    // With a speaker, those who do not see it do not hear it or its sounds either, as ModernUO.
    private int SendAround(MapType map, Point3D location, IOutgoingPacket packet, MobileEntity? speaker = null)
    {
        var sent = 0;

        foreach (var session in _sessions.GetAll())
        {
            if (session.CharacterId.IsValid &&
                _mobiles.TryGet(session.CharacterId, out var listener) &&
                listener.Map == map &&
                listener.Location.InRange(location, SayRange) &&
                speaker?.IsHiddenFrom(listener.Id, session.AccountType) != true &&
                session.NetworkSession.Client is { IsConnected: true } connection &&
                _sender.TrySend(session.SessionId, connection, packet))
            {
                sent++;
            }
        }

        return sent;
    }
}
