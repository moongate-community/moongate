using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Events;

/// <summary>
/// Default outbound gameplay dispatcher for mobile updates and speech.
/// </summary>
public sealed class DispatchEventsService : IDispatchEventsService
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchEventsService" /> class.
    /// </summary>
    public DispatchEventsService(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    /// <inheritdoc />
    public Task<int> DispatchMobileUpdateAsync(
        UOMobileEntity mobile,
        int mapId,
        int range,
        bool isNew,
        bool stygianAbyss = true,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(mobile);

        var players = _spatialWorldService.GetPlayersInRange(mobile.Location, Math.Max(0, range), mapId);
        var recipients = 0;

        foreach (var playerSession in players)
        {
            if (playerSession.CharacterId == mobile.Id || playerSession.Character is null)
            {
                continue;
            }

            if (isNew)
            {
                _outgoingPacketQueue.Enqueue(
                    playerSession.SessionId,
                    new MobileIncomingPacket(playerSession.Character, mobile, stygianAbyss, true)
                );
                _outgoingPacketQueue.Enqueue(playerSession.SessionId, new PlayerStatusPacket(mobile, 1));
                WornItemPacketHelper.EnqueueVisibleWornItems(
                    mobile,
                    packet => _outgoingPacketQueue.Enqueue(playerSession.SessionId, packet)
                );
            }
            else
            {
                _outgoingPacketQueue.Enqueue(playerSession.SessionId, new MobileMovingPacket(mobile, stygianAbyss));
            }

            recipients++;
        }

        return Task.FromResult(recipients);
    }

    /// <inheritdoc />
    public Task<int> DispatchMobileSpeechAsync(
        UOMobileEntity speaker,
        string text,
        int range,
        ChatMessageType messageType = ChatMessageType.Regular,
        short hue = SpeechHues.Default,
        short font = SpeechHues.DefaultFont,
        string language = "ENU",
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        ArgumentNullException.ThrowIfNull(speaker);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(0);
        }

        var packet = SpeechMessageFactory.CreateFromSpeaker(
            speaker,
            messageType,
            hue,
            font,
            language,
            text
        );
        var recipients = _spatialWorldService.GetPlayersInRange(
            speaker.Location,
            Math.Max(0, range),
            speaker.MapId
        );

        foreach (var session in recipients)
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, packet);
        }

        return Task.FromResult(recipients.Count);
    }
}
