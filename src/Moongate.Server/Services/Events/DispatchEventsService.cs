using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Events;

/// <summary>
/// Default outbound gameplay dispatcher for mobile updates and speech.
/// </summary>
[RegisterGameEventListener]
public sealed class DispatchEventsService
    : IDispatchEventsService,
      IGameEventListener<MobileWarModeChangedEvent>,
      IGameEventListener<MobilePlaySoundEvent>,
      IGameEventListener<PlaySoundToPlayerEvent>,
      IGameEventListener<MobilePlayEffectEvent>,
      IGameEventListener<PlayEffectToPlayerEvent>
{
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchEventsService" /> class.
    /// </summary>
    public DispatchEventsService(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
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

    /// <inheritdoc />
    public Task<int> DispatchMobileSoundAsync(
        int mapId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0,
        int? range = null,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var packet = new PlaySoundEffectPacket(mode, soundModel, unknown3, location);

        return _spatialWorldService.BroadcastToPlayersAsync(packet, mapId, location, range);
    }

    /// <inheritdoc />
    public Task HandleAsync(MobileWarModeChangedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        return _spatialWorldService.BroadcastToPlayersAsync(
            new MobileMovingPacket(gameEvent.Mobile, true),
            gameEvent.Mobile.MapId,
            gameEvent.Mobile.Location
        );
    }

    /// <inheritdoc />
    public Task HandleAsync(MobilePlaySoundEvent gameEvent, CancellationToken cancellationToken = default)
        => DispatchMobileSoundAsync(
            gameEvent.MapId,
            gameEvent.Location,
            gameEvent.SoundModel,
            gameEvent.Mode,
            gameEvent.Unknown3,
            null,
            cancellationToken
        );

    /// <inheritdoc />
    public Task<bool> DispatchSoundToPlayerAsync(
        Serial characterId,
        Point3D location,
        ushort soundModel,
        byte mode = 0x01,
        ushort unknown3 = 0,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!_gameNetworkSessionService.TryGetByCharacterId(characterId, out var session))
        {
            return Task.FromResult(false);
        }

        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new PlaySoundEffectPacket(mode, soundModel, unknown3, location)
        );

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task HandleAsync(PlaySoundToPlayerEvent gameEvent, CancellationToken cancellationToken = default)
        => DispatchSoundToPlayerAsync(
            gameEvent.CharacterId,
            gameEvent.Location,
            gameEvent.SoundModel,
            gameEvent.Mode,
            gameEvent.Unknown3,
            cancellationToken
        );

    public Task<int> DispatchMobileEffectAsync(
        int mapId,
        Point3D location,
        ushort itemId,
        byte speed = 10,
        byte duration = 10,
        int hue = 0,
        int renderMode = 0,
        ushort effect = 0,
        ushort explodeEffect = 0,
        ushort explodeSound = 0,
        byte layer = 0xFF,
        ushort unknown3 = 0,
        int? range = null,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        var packet = EffectsFactory.CreateParticle(
            directionType: EffectDirectionType.StayAtLocation,
            itemId: itemId,
            sourceId: Serial.Zero,
            targetId: Serial.Zero,
            sourceLocation: location,
            targetLocation: location,
            speed: speed,
            duration: duration,
            fixedDirection: true,
            explode: false,
            hue: hue,
            renderMode: renderMode,
            effect: effect,
            explodeEffect: explodeEffect,
            explodeSound: explodeSound,
            effectSerial: Serial.Zero,
            layer: layer,
            unknown3: unknown3
        );

        return _spatialWorldService.BroadcastToPlayersAsync(packet, mapId, location, range);
    }

    public Task HandleAsync(MobilePlayEffectEvent gameEvent, CancellationToken cancellationToken = default)
        => DispatchMobileEffectAsync(
            gameEvent.MapId,
            gameEvent.Location,
            gameEvent.ItemId,
            gameEvent.Speed,
            gameEvent.Duration,
            gameEvent.Hue,
            gameEvent.RenderMode,
            gameEvent.Effect,
            gameEvent.ExplodeEffect,
            gameEvent.ExplodeSound,
            gameEvent.Layer,
            gameEvent.Unknown3,
            null,
            cancellationToken
        );

    public Task<bool> DispatchEffectToPlayerAsync(
        Serial characterId,
        Point3D location,
        ushort itemId,
        byte speed = 10,
        byte duration = 10,
        int hue = 0,
        int renderMode = 0,
        ushort effect = 0,
        ushort explodeEffect = 0,
        ushort explodeSound = 0,
        byte layer = 0xFF,
        ushort unknown3 = 0,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!_gameNetworkSessionService.TryGetByCharacterId(characterId, out var session))
        {
            return Task.FromResult(false);
        }

        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            EffectsFactory.CreateParticle(
                directionType: EffectDirectionType.StayAtLocation,
                itemId: itemId,
                sourceId: Serial.Zero,
                targetId: Serial.Zero,
                sourceLocation: location,
                targetLocation: location,
                speed: speed,
                duration: duration,
                fixedDirection: true,
                explode: false,
                hue: hue,
                renderMode: renderMode,
                effect: effect,
                explodeEffect: explodeEffect,
                explodeSound: explodeSound,
                effectSerial: Serial.Zero,
                layer: layer,
                unknown3: unknown3
            )
        );

        return Task.FromResult(true);
    }

    public Task HandleAsync(PlayEffectToPlayerEvent gameEvent, CancellationToken cancellationToken = default)
        => DispatchEffectToPlayerAsync(
            gameEvent.CharacterId,
            gameEvent.Location,
            gameEvent.ItemId,
            gameEvent.Speed,
            gameEvent.Duration,
            gameEvent.Hue,
            gameEvent.RenderMode,
            gameEvent.Effect,
            gameEvent.ExplodeEffect,
            gameEvent.ExplodeSound,
            gameEvent.Layer,
            gameEvent.Unknown3,
            cancellationToken
        );
}
