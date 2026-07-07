namespace Moongate.UO.Data.Types;

/// <summary>
/// Identity of a UO map facet. The value matches the map index used by the client data files.
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
