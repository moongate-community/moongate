namespace Moongate.Persistence.Types.Persistence;

/// <summary>
///     Identifies an independently configured PostgreSQL persistence database.
/// </summary>
public enum PersistenceDatabaseTarget
{
    /// <summary>
    ///     The shared account database.
    /// </summary>
    Accounts = 0,

    /// <summary>
    ///     The database owned by one realm process.
    /// </summary>
    Realm = 1
}
