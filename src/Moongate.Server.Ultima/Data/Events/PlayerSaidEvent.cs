using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a player's character said something aloud and the players and NPCs around heard it, aloud, as an emote, in a whisper or in a yell. A command
///     (text starting with a dot) is not speech and publishes nothing.
/// </summary>
public sealed record PlayerSaidEvent(MobileEntity Speaker, string Text, SpeechType Type = SpeechType.Regular) : IMoongateEvent;
