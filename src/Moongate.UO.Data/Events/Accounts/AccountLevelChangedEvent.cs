using Moongate.Core.Server.Types;

namespace Moongate.UO.Data.Events.Accounts;

public record AccountLevelChangedEvent(string AccountId, AccountLevelType Level);
