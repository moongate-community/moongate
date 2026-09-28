using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a player's character left the world because its session closed; <see cref="Character" /> is the
///     copy that was saved, as it was when it left.
/// </summary>
public sealed record CharacterLeftWorldEvent(MobileEntity Character) : IMoongateEvent;
