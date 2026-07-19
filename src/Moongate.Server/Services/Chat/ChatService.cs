using Moongate.Server.Data.Internal.Chat;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Chat;

/// <summary>
/// Default <see cref="IChatService" />. <see cref="Classify" />/<see cref="IsRateLimited" /> are the
/// pure decision core — public and static so they are unit-testable without a live
/// <see cref="Moongate.Server.Abstractions.Data.Session.PlayerSession" />, mirroring
/// <c>MovementService.Evaluate</c>/<c>WorldService.IsRecipient</c>.
/// </summary>
public sealed class ChatService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(25);

    private const int DefaultRange = 15;
    private const int YellRange = 18;
    private const int WhisperRange = 1;

    public static ChatDecision Classify(string rawText)
    {
        if (rawText.StartsWith('.'))
        {
            return new(true, ChatMessageType.Command, rawText, 0);
        }

        if (rawText.Length >= 2 && rawText[0] == '*' && rawText[^1] == '*')
        {
            return new(false, ChatMessageType.Emote, rawText[1..^1], DefaultRange);
        }

        if (rawText.StartsWith('!'))
        {
            return new(false, ChatMessageType.Yell, rawText[1..], YellRange);
        }

        if (rawText.StartsWith(';'))
        {
            return new(false, ChatMessageType.Whisper, rawText[1..], WhisperRange);
        }

        return new(false, ChatMessageType.Regular, rawText, DefaultRange);
    }

    public static bool IsRateLimited(DateTimeOffset lastChatAt, DateTimeOffset now)
        => now - lastChatAt < MinInterval;
}
