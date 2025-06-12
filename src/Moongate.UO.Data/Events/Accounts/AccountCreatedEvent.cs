using Moongate.Core.Server.Types;

namespace Moongate.UO.Data.Events.Accounts;

public record AccountCreatedEvent(string Id, string AccountName, AccountLevelType Level);
