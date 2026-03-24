namespace Moongate.Server.Types.Items;

/// <summary>
/// Selects how loot generation gates and persists generated container content.
/// </summary>
public enum LootGenerationMode : byte
{
    /// <summary>
    /// Generates loot lazily the first time a container is opened, with refill semantics.
    /// </summary>
    FirstOpen = 0,

    /// <summary>
    /// Generates loot immediately for a corpse or death container without first-open gating.
    /// </summary>
    OnDeath = 1
}
