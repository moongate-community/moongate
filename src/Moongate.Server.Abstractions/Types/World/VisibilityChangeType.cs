namespace Moongate.Server.Abstractions.Types.World;

/// <summary>What one object's change meant for one client.</summary>
public enum VisibilityChangeType : byte
{
    /// <summary>Out of range and unknown: the client neither had it nor needs it.</summary>
    None = 0,

    /// <summary>It came into range and was drawn.</summary>
    Drawn,

    /// <summary>It left range and the client was told to forget it.</summary>
    Undrawn,

    /// <summary>It was already known and stayed in range: a position update, not a redraw.</summary>
    Moved
}
