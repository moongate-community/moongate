namespace Moongate.Server.Types.Magic;

/// <summary>
/// Identifies the resolved target payload kind bound to a spell cast.
/// </summary>
public enum SpellTargetKind : byte
{
    None = 0,
    Mobile = 1,
    Item = 2,
    Location = 3
}
