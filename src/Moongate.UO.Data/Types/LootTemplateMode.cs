namespace Moongate.UO.Data.Types;

/// <summary>
/// Controls how a loot table generates entries at runtime.
/// </summary>
public enum LootTemplateMode : byte
{
    Weighted = 0,
    Additive = 1
}
