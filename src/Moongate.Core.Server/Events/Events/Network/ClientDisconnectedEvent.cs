namespace Moongate.Core.Server.Events.Events.Network;

public record ClientDisconnectedEvent(string ServerId, string SessionId);
