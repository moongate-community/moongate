using Moongate.Core.Primitives;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised whenever a mobile's speech is broadcast, regardless of caller (packet or Lua). Lets NPC/
/// script systems react to nearby speech without touching the network layer — auto-exposed to Lua
/// as <c>events.on("mobile_speech", ...)</c> by the existing event-bus-to-Lua bridge.
/// </summary>
public sealed record MobileSpeechEvent(Serial Speaker, ChatMessageType Type, string Text) : IEvent;
