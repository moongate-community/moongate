using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Data.Events;

/// <summary>
/// Raised after a player character has been deleted, along with everything it owned, and unlinked from
/// its account. <see cref="Character" /> is the mobile as it was: by the time this is raised it is gone
/// from the store, so subscribers that need its state must read it from here.
/// </summary>
public sealed record CharacterDeletedEvent(Serial AccountId, Serial MobileId, MobileEntity Character) : IEvent;
