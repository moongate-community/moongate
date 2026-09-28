namespace Moongate.Server.Ultima.Data.Motd;

/// <summary>
///     Immutable values available when one character receives a MOTD.
/// </summary>
public sealed record MotdContext(
    string ServerName,
    string RealmName,
    string Version,
    string Codename,
    string PlayerName,
    int UsersOnline
);
