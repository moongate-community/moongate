using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Events.Characters;

public record CharacterLoggedEvent(string SessionId, Serial CharacterSerial, string CharacterName);

