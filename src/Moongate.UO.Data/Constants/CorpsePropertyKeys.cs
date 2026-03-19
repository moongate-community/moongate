namespace Moongate.UO.Data.Constants;

/// <summary>
/// Shared corpse metadata keys and constants used by packet and server logic.
/// </summary>
public static class CorpsePropertyKeys
{
    public const int ItemId = 0x2006;
    public const string IsCorpse = "is_corpse";
    public const string EquippedLayer = "corpse_equipped_layer";
    public const string OwnerMobileId = "corpse_owner_mobile_id";
    public const string DecayType = "decay_type";
}
