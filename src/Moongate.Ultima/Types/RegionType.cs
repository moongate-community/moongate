namespace Moongate.Ultima.Types;

/// <summary>
///     What a region is. The rules a region applies are listed as flags in the region files; the type says what
///     kind of place it is, for behavior that is not a simple rule, such as a dungeon warning for young players.
/// </summary>
public enum RegionType : byte
{
    Base = 0,
    Town = 1,
    Dungeon = 2,
    NoHousing = 3,
    Guarded = 4,
    Jail = 5,
    GreenAcres = 6
}
