namespace Moongate.UO.Data.Types;

/// <summary>
/// How the client tints the target cursor, and the one value that is not a colour:
/// <see cref="Cancel" /> takes the cursor down instead of raising it.
/// </summary>
public enum TargetCursorType : byte
{
    Neutral = 0,

    Harmful = 1,

    Beneficial = 2,

    /// <summary>Sent by the server to withdraw a cursor it raised.</summary>
    Cancel = 3
}
