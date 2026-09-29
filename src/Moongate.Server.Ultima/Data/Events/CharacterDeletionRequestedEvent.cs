using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a player asked to delete a character and the request was saved; the character stays
///     restorable until it is removed.
/// </summary>
public sealed record CharacterDeletionRequestedEvent(MobileEntity Character) : IMoongateEvent;
