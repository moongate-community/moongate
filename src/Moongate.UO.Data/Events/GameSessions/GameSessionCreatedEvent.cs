using Moongate.UO.Data.Session;

namespace Moongate.UO.Data.Events.GameSessions;

public record GameSessionCreatedEvent(string SessionId, GameSession Session);
