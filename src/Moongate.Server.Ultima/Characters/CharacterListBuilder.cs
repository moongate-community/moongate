using Moongate.Core.Types.Expansions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Server.Ultima.Characters;

/// <summary>
///     Lays out an account's characters for the character list (0xA9) and picks the slot flags that go with the limit.
/// </summary>
public static class CharacterListBuilder
{
    /// <summary>
    ///     Returns <paramref name="maxPerAccount" /> names, each character at its slot. A character without a slot, beyond
    ///     the limit or sharing a slot takes the first free position; characters that do not fit are left out.
    /// </summary>
    public static string?[] Names(IReadOnlyList<MobileEntity> characters, int maxPerAccount)
    {
        var names = new string?[maxPerAccount];
        var unplaced = new List<MobileEntity>();

        foreach (var character in characters)
        {
            if (character.Slot is { } slot && slot < maxPerAccount && names[slot] is null)
            {
                names[slot] = character.Name;
            }
            else
            {
                unplaced.Add(character);
            }
        }

        foreach (var character in unplaced)
        {
            var free = Array.IndexOf(names, null);

            if (free < 0)
            {
                break;
            }

            names[free] = character.Name;
        }

        return names;
    }

    /// <summary>
    ///     The character-list flags that tell the client how many slots to show, added to
    ///     <see cref="CharacterListFlags.Default" />.
    /// </summary>
    public static CharacterListFlags SlotFlags(int maxPerAccount)
    {
        return maxPerAccount switch
        {
            1 => CharacterListFlags.OneCharacterSlot | CharacterListFlags.SlotLimit,
            6 => CharacterListFlags.SixthCharacterSlot,
            7 => CharacterListFlags.SixthCharacterSlot | CharacterListFlags.SeventhCharacterSlot,
            _ => CharacterListFlags.None
        };
    }

    /// <summary>
    ///     The supported features (0xB9) of the targeted expansion plus the extra character slots of the limit.
    /// </summary>
    public static FeatureFlags Features(int maxPerAccount)
    {
        var features = FeatureFlags.ExpansionEj;

        if (maxPerAccount >= 6)
        {
            features |= FeatureFlags.SixthCharacterSlot;
        }

        if (maxPerAccount >= 7)
        {
            features |= FeatureFlags.SeventhCharacterSlot;
        }

        return features;
    }
}
