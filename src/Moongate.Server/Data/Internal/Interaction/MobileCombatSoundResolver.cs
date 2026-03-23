using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Interaction;

/// <summary>
/// Resolves mobile combat sounds with runtime overrides and global fallbacks.
/// </summary>
public sealed class MobileCombatSoundResolver
{
    private readonly IReadOnlyDictionary<MobileSoundType, int> _fallbacks = new Dictionary<MobileSoundType, int>
    {
        [MobileSoundType.StartAttack] = 0x023B,
        [MobileSoundType.Attack] = 0x023B,
        [MobileSoundType.Defend] = 0x0140
    };

    public bool TryResolveHitSound(UOMobileEntity attacker, out int soundId)
    {
        ArgumentNullException.ThrowIfNull(attacker);

        if (TryResolveWeaponSound(attacker, static item => item.HitSound, out soundId))
        {
            return true;
        }

        return TryResolve(attacker, MobileSoundType.Attack, out soundId);
    }

    public bool TryResolveMissSound(UOMobileEntity attacker, out int soundId)
    {
        ArgumentNullException.ThrowIfNull(attacker);

        if (TryResolveWeaponSound(attacker, static item => item.MissSound, out soundId))
        {
            return true;
        }

        return TryResolve(attacker, MobileSoundType.Attack, out soundId);
    }

    public bool TryResolve(UOMobileEntity mobile, MobileSoundType type, out int soundId)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.TryGetSound(type, out soundId))
        {
            return true;
        }

        return _fallbacks.TryGetValue(type, out soundId);
    }

    private static bool TryResolveWeaponSound(
        UOMobileEntity attacker,
        Func<UOItemEntity, int?> selector,
        out int soundId
    )
    {
        foreach (var equippedItem in attacker.GetEquippedItemsRuntime())
        {
            if (equippedItem.EquippedLayer is not (ItemLayerType.OneHanded or ItemLayerType.TwoHanded))
            {
                continue;
            }

            if (!equippedItem.WeaponSkill.HasValue)
            {
                continue;
            }

            var candidate = selector(equippedItem);

            if (!candidate.HasValue)
            {
                continue;
            }

            soundId = candidate.Value;

            return true;
        }

        soundId = default;

        return false;
    }
}
