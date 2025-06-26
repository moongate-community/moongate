using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Session;

namespace Moongate.UO.Data.Events.Characters;

public record CharacterInGameEvent(GameSession gameSession, UOMobileEntity mobile);

