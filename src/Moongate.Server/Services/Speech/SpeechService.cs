using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Speech;

/// <summary>
/// Centralizes processing of inbound speech packets.
/// </summary>
public sealed class SpeechService : ISpeechService
{
    private const int DefaultNpcHearingRange = 12;
    private const int DefaultSpeechRange = 12;
    private const int WhisperSpeechRange = 1;
    private const int YellSpeechRange = 18;
    private readonly ICommandSystemService _commandSystemService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IDispatchEventsService _dispatchEventsService;

    public SpeechService(
        ICommandSystemService commandSystemService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService,
        IGameEventBusService gameEventBusService,
        ISpatialWorldService spatialWorldService,
        IDispatchEventsService dispatchEventsService
    )
    {
        _commandSystemService = commandSystemService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
        _gameEventBusService = gameEventBusService;
        _spatialWorldService = spatialWorldService;
        _dispatchEventsService = dispatchEventsService;
    }

    public async Task<int> BroadcastFromServerAsync(
        string text,
        short hue = SpeechHues.System,
        short font = SpeechHues.DefaultFont,
        string language = "ENU"
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var recipients = 0;

        foreach (var session in _gameNetworkSessionService.GetAll())
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, SpeechMessageFactory.CreateSystem(text, hue, font, language));
            recipients++;
        }

        await _gameEventBusService.PublishAsync(
            new BroadcastFromServerEvent(
                GameEventBase.CreateNow(),
                text,
                hue,
                font,
                language,
                recipients
            )
        );

        return recipients;
    }

    public async Task HandleOpenChatWindowAsync(
        GameSession session,
        OpenChatWindowPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        var chatName = session.Character?.Name;

        if (string.IsNullOrWhiteSpace(chatName))
        {
            chatName = $"User{session.SessionId}";
        }

        _outgoingPacketQueue.Enqueue(
            session.SessionId,
            new ChatCommandPacket(ChatCommandType.OpenChatWindow, chatName)
        );

        await _gameEventBusService.PublishAsync(
            new OpenChatWindowRequestedEvent(session.SessionId, packet.Payload.ToArray()),
            cancellationToken
        );
    }

    public async Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
        GameSession session,
        UnicodeSpeechPacket speechPacket,
        CancellationToken cancellationToken = default
    )
    {
        var text = speechPacket.Text.Trim();

        if (text.Length == 0)
        {
            return null;
        }

        if (text.StartsWith('.'))
        {
            var commandText = speechPacket.Text[1..];
            await _commandSystemService.ExecuteCommandAsync(
                commandText,
                CommandSourceType.InGame,
                session,
                cancellationToken
            );

            return null;
        }

        if (session.Character is null)
        {
            return null;
        }

        var effectiveMessageType = ResolveIncomingMessageType(speechPacket.MessageType, text);
        text = NormalizeIncomingText(effectiveMessageType, text);
        var speechRange = ResolveSpeechRange(effectiveMessageType);

        await _dispatchEventsService.DispatchMobileSpeechAsync(
            session.Character,
            text,
            speechRange,
            effectiveMessageType,
            speechPacket.Hue,
            speechPacket.Font,
            speechPacket.Language,
            cancellationToken
        );

        await PublishSpeechHeardEventsAsync(session, text, effectiveMessageType, cancellationToken);

        return null;
    }

    public async Task<bool> SendMessageFromServerAsync(
        GameSession session,
        string text,
        short hue = SpeechHues.System,
        short font = SpeechHues.DefaultFont,
        string language = "ENU"
    )
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, SpeechMessageFactory.CreateSystem(text, hue, font, language));
        await _gameEventBusService.PublishAsync(
            new SendMessageFromServerEvent(
                GameEventBase.CreateNow(),
                session.SessionId,
                text,
                hue,
                font,
                language
            )
        );

        return true;
    }

    public async Task<int> SpeakAsMobileAsync(
        UOMobileEntity speaker,
        string text,
        int range = 12,
        ChatMessageType messageType = ChatMessageType.Regular,
        short hue = SpeechHues.Default,
        short font = SpeechHues.DefaultFont,
        string language = "ENU",
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(speaker);

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var normalizedRange = Math.Max(0, range);
        var recipients = await _dispatchEventsService.DispatchMobileSpeechAsync(
                             speaker,
                             text,
                             normalizedRange,
                             messageType,
                             hue,
                             font,
                             language,
                             cancellationToken
                         );
        var packet = SpeechMessageFactory.CreateFromSpeaker(
            speaker,
            messageType,
            hue,
            font,
            language,
            text
        );

        await _gameEventBusService.PublishAsync(
            new MobileSpokeEvent(
                speaker.Id,
                speaker.MapId,
                text,
                messageType,
                packet.Hue,
                packet.Font,
                packet.Language,
                normalizedRange,
                recipients
            ),
            cancellationToken
        );

        return recipients;
    }

    private async Task PublishSpeechHeardEventsAsync(
        GameSession session,
        string text,
        ChatMessageType speechType,
        CancellationToken cancellationToken
    )
    {
        if (session.Character is null)
        {
            return;
        }

        var speaker = session.Character;
        var nearbyMobiles = _spatialWorldService.GetNearbyMobiles(
            speaker.Location,
            DefaultNpcHearingRange,
            speaker.MapId
        );

        foreach (var mobile in nearbyMobiles)
        {
            if (mobile.IsPlayer || mobile.Id == speaker.Id)
            {
                continue;
            }

            await _gameEventBusService.PublishAsync(
                new SpeechHeardEvent(
                    mobile.Id,
                    speaker.Id,
                    text,
                    speechType,
                    speaker.MapId,
                    speaker.Location
                ),
                cancellationToken
            );
        }
    }

    private static ChatMessageType ResolveIncomingMessageType(ChatMessageType messageType, string text)
    {
        if (messageType != ChatMessageType.Regular)
        {
            return messageType;
        }

        if (IsAsteriskWrappedEmote(text))
        {
            return ChatMessageType.Emote;
        }

        return text[0] switch
        {
            '!' when text.Length > 1 => ChatMessageType.Yell,
            ';' when text.Length > 1 => ChatMessageType.Whisper,
            _ => messageType
        };
    }

    private static string NormalizeIncomingText(ChatMessageType messageType, string text)
        => messageType switch
        {
            ChatMessageType.Yell when text[0] == '!' => text[1..].TrimStart(),
            ChatMessageType.Whisper when text[0] == ';' => text[1..].TrimStart(),
            _ => text
        };

    private static bool IsAsteriskWrappedEmote(string text)
        => text.Length >= 3 &&
           text[0] == '*' &&
           text[^1] == '*' &&
           !string.IsNullOrWhiteSpace(text[1..^1]);

    private static int ResolveSpeechRange(ChatMessageType messageType)
        => messageType switch
        {
            ChatMessageType.Whisper => WhisperSpeechRange,
            ChatMessageType.Yell => YellSpeechRange,
            _ => DefaultSpeechRange
        };
}
