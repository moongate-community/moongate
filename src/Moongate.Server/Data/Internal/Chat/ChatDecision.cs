using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Chat;

/// <summary>The pure result of classifying one raw speech string, before any side effect is applied.</summary>
public readonly record struct ChatDecision(bool IsCommand, ChatMessageType Type, string Text, int Range);
