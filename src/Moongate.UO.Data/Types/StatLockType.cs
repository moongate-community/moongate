namespace Moongate.UO.Data.Types;

/// <summary>
/// Whether a stat may drift as skills are used: the up/down/lock arrows the client shows next to
/// Strength, Dexterity and Intelligence in the status gump. Only a stat set to <see cref="Up" /> can
/// be gained; <see cref="Down" /> lets it fall to make room under the stat cap.
/// </summary>
public enum StatLockType : byte
{
    Up = 0,
    Down = 1,
    Locked = 2
}
