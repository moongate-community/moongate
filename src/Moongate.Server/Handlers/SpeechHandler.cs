using Moongate.Network.Packets.Incoming;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Services.Chat;
using Serilog;

namespace Moongate.Server.Handlers;

/// <summary>
/// Handles player speech (0xAD): rate-limits, classifies the raw text (say/emote/whisper/yell/
/// command) via <see cref="ChatService" />'s pure core, and either dispatches the "." prefix to
/// <see cref="ICommandService.Execute" /> or hands the classified message to
/// <see cref="IChatService.Say" />. Packets with the classic-client "encoded" (keyword-menu) flag
/// are dropped, as is any text that is empty or longer than 128 characters after trimming.
/// </summary>
public sealed class SpeechHandler : IPacketHandler<UnicodeSpeechPacket>, IPacketHandlerRegistration
{
    private const int MaxTextLength = 128;

    private readonly ILogger _logger = Log.ForContext<SpeechHandler>();
    private readonly IChatService _chat;
    private readonly ICommandService _commands;

    public SpeechHandler(IChatService chat, ICommandService commands)
    {
        _chat = chat;
        _commands = commands;
    }

    public void Handle(UnicodeSpeechPacket packet, in PacketContext context)
    {
        var session = context.Session;

        if (session.Character is not { } speaker)
        {
            return;
        }

        if (packet.IsEncoded)
        {
            _logger.Debug("Dropped encoded speech packet from session {SessionId}", session.SessionId);

            return;
        }

        if (packet.Text.Length is 0 or > MaxTextLength)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        if (ChatService.IsRateLimited(session.LastChatAt, now))
        {
            return;
        }

        session.SetLastChat(now);

        var decision = ChatService.Classify(packet.Text);

        if (decision.IsCommand)
        {
            _commands.Execute(session, speaker, decision.Text);

            return;
        }

        if (decision.Text.Length == 0)
        {
            return;
        }

        _chat.Say(speaker, decision.Type, decision.Text, packet.Hue, decision.Range);
    }

    public void Register(INetworkService network)
        => network.RegisterHandler(this);
}
