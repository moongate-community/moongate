namespace Moongate.Ultima.Types;

/// <summary>
///     Facets the UO client can display, valued with the map id sent on the wire.
/// </summary>
public enum MapType : byte
{
    Felucca = 0,
    Trammel = 1,
    Ilshenar = 2,
    Malas = 3,
    Tokuno = 4,
    TerMur = 5
}
