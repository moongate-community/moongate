using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a player's character said something aloud and the players and NPCs around heard it. A command
///     (text starting with a dot) is not speech and publishes nothing.
/// </summary>
public sealed record PlayerSaidEvent(MobileEntity Speaker, string Text) : IMoongateEvent;
