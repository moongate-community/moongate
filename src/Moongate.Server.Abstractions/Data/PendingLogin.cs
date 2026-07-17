namespace Moongate.Server.Abstractions.Data;

/// <summary>An authenticated login awaiting the client's reconnect on the game port.</summary>
public readonly record struct PendingLogin(string Username);
