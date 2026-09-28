using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a player's character entered the world and the client was told its login is complete (0x55).
/// </summary>
public sealed record CharacterEnteredWorldEvent(MobileEntity Character) : IMoongateEvent;
