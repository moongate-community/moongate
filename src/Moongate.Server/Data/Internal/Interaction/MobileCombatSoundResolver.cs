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

    public bool TryResolve(UOMobileEntity mobile, MobileSoundType type, out int soundId)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.TryGetSound(type, out soundId))
        {
            return true;
        }

        return _fallbacks.TryGetValue(type, out soundId);
    }
}
