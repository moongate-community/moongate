using Moongate.UO.Data.Events.Contexts;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Events.Characters;

public record CharacterCreatedEvent(string Account, UOMobileEntity Mobile, UoEventContext Context);

