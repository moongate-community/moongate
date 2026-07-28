using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Support;

/// <summary>Records chat mutations at the final <see cref="IChatService" /> boundary.</summary>
public sealed class RecordingChatService : IChatService
{
    private readonly List<(string Text, Hue? Hue)> _broadcasts = [];
    private readonly List<(Serial Speaker, ChatMessageType Type, string Text, Hue Hue, int Range)> _messages = [];

    public IReadOnlyList<(string Text, Hue? Hue)> Broadcasts => _broadcasts;

    public IReadOnlyList<(Serial Speaker, ChatMessageType Type, string Text, Hue Hue, int Range)> Messages => _messages;

    public void Broadcast(string text, Hue? hue = null)
        => _broadcasts.Add((text, hue));

    public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
        => _messages.Add((speaker.Id, type, text, hue, range));
}
