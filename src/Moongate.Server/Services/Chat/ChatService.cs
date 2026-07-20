using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Data.Internal.Chat;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.Chat;

/// <summary>
/// Default <see cref="IChatService" />. <see cref="Classify" />/<see cref="IsRateLimited" /> are the
/// pure decision core — public and static so they are unit-testable without a live
/// <see cref="Moongate.Server.Abstractions.Data.Session.PlayerSession" />, mirroring
/// <c>MovementService.Evaluate</c>/<c>WorldService.IsRecipient</c>. <see cref="Say" />/
/// <see cref="Broadcast" /> are the impure orchestrator: build the outgoing packet, send it, publish
/// the event.
/// </summary>
public sealed class ChatService : IChatService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(25);

    private const int DefaultRange = 15;
    private const int YellRange = 18;
    private const int WhisperRange = 1;

    private readonly IWorldService _world;
    private readonly IEventBus _events;

    public ChatService(IWorldService world, IEventBus events)
    {
        _world = world;
        _events = events;
    }

    public void Broadcast(string text, Hue? hue = null)
        => _world.Broadcast(ChatMessageFactory.CreateSystem(text, hue));

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

    public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
    {
        var packet = ChatMessageFactory.CreateFromMobile(speaker.Id, speaker.Name, speaker.Body, type, hue, text);
        _world.SendToPlayersInRange(speaker.MapId, speaker.Position, range, packet);
        _events.Publish(new MobileSpeechEvent(speaker.Id, type, text));
    }
}
