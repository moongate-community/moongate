namespace Moongate.UO.Data.Types;

/// <summary>What the client is being asked to pick with a target cursor.</summary>
public enum TargetSelectionType : byte
{
    /// <summary>An entity — a mobile or an item.</summary>
    Object = 0,

    /// <summary>A spot on the ground.</summary>
    Location = 1
}
