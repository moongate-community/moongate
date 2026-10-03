using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Prompts;

/// <summary>
///     Hands the player's answer to a text prompt (0xC2) to the prompt it was asked with: the text without the spaces
///     around it, or nothing when the player escaped or typed only spaces. As ModernUO, a text over 128 characters is ignored; so is an
///     answer to another prompt, or with none pending.
/// </summary>
public sealed class TextPromptResponsePacketHandler : IPacketHandler<TextPromptResponsePacket>
{
    public const int MaximumTextLength = 128;

    private readonly ILogger _logger = Log.ForContext<TextPromptResponsePacketHandler>();
    private readonly IPromptService _prompts;

    public TextPromptResponsePacketHandler(IPromptService prompts)
    {
        _prompts = prompts;
    }

    public void Handle(GameSession session, TextPromptResponsePacket packet)
    {
        if (packet.Text.Length > MaximumTextLength)
        {
            _logger.Debug("Session {SessionId} answered a prompt with {Length} characters", session.SessionId, packet.Text.Length);

            return;
        }

        // A text of spaces only is no answer, as an escape.
        var text = packet.IsCancel || string.IsNullOrWhiteSpace(packet.Text) ? null : packet.Text.Trim();

        if (!_prompts.TryComplete(session, packet.PromptId, text))
        {
            _logger.Debug("Session {SessionId} answered prompt {PromptId}, which is not pending", session.SessionId, packet.PromptId);
        }
    }
}
