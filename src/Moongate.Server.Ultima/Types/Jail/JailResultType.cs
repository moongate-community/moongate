namespace Moongate.Server.Ultima.Types.Jail;

/// <summary>
///     What the jail answered to a request to send a mobile to a cell.
/// </summary>
public enum JailResultType
{
    /// <summary>The mobile is in the cell.</summary>
    Ok = 0,

    /// <summary>There is no <c>data/jail.toml</c>.</summary>
    Disabled = 1,

    /// <summary>No cell has that number.</summary>
    NoSuchCell = 2,

    /// <summary>Someone else is in the cell.</summary>
    CellOccupied = 3,

    /// <summary>The days are below 1 or above the longest sentence.</summary>
    BadDays = 4,

    /// <summary>The mobile is not in the world.</summary>
    NotInWorld = 5,

    /// <summary>The mobile is the one who jails, or a player of its rank or above.</summary>
    Refused = 6,

    /// <summary>The map of the jail is not loaded, or the cell is outside it.</summary>
    MapNotLoaded = 7
}
