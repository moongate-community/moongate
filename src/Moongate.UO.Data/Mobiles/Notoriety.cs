using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Mobiles;

/// <summary>
/// Derives the notoriety a mobile is shown with from its murder count and criminal flag, in ModernUO's
/// precedence: murderer beats criminal beats innocent.
/// </summary>
/// <remarks>
/// ModernUO resolves notoriety pairwise (source against target), so guild, party and aggressor state can
/// change the answer per observer. This is the absolute, self-only view, which is all the enter-world
/// burst needs; the pairwise rules arrive with the nearby-mobile broadcast.
/// </remarks>
public static class Notoriety
{
    /// <summary>Murder count from which a mobile counts as a murderer.</summary>
    public const int MurdererKills = 5;

    /// <summary>True when <paramref name="kills" /> has reached the murderer threshold.</summary>
    public static bool IsMurderer(int kills)
        => kills >= MurdererKills;

    public static NotorietyType Resolve(int kills, bool criminal)
    {
        if (IsMurderer(kills))
        {
            return NotorietyType.Murderer;
        }

        return criminal ? NotorietyType.Criminal : NotorietyType.Innocent;
    }
}
