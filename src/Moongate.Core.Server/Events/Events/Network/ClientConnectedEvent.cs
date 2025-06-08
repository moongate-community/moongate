namespace Moongate.Core.Server.Events.Events.Network;

public record ClientConnectedEvent(string ServerId, string SessionId);
