namespace Moongate.Ultima.Types;

/// <summary>
///     What kind of creature a body is, which decides how it animates and what it can wear.
/// </summary>
public enum BodyType : byte
{
    Empty = 0,
    Monster = 1,
    Sea = 2,
    Animal = 3,
    Human = 4,
    Equipment = 5
}
