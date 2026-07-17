using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>Raised after a new player character has been created, persisted and linked to its account.</summary>
public sealed record CharacterCreatedEvent(Serial AccountId, Serial MobileId, MobileEntity Character) : IEvent;
