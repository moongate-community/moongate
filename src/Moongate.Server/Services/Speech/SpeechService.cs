using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Speech;

/// <summary>
/// Centralizes processing of inbound speech packets.
/// </summary>
public sealed class SpeechService : ISpeechService
{
    private readonly ICommandSystemService _commandSystemService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public SpeechService(
        ICommandSystemService commandSystemService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _commandSystemService = commandSystemService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public int BroadcastFromServer(
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

        return recipients;
    }

    public bool SendMessageFromServer(
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

        return true;
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

        return SpeechMessageFactory.CreateFromSpeaker(
            session.Character,
            speechPacket.MessageType,
            speechPacket.Hue,
            speechPacket.Font,
            speechPacket.Language,
            text
        );
    }
}
