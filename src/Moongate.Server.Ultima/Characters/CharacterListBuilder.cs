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
        return Layout(characters, maxPerAccount).Select(character => character?.Name).ToArray();
    }

    /// <summary>
    ///     Returns <paramref name="maxPerAccount" /> positions with the character shown in each, laid out as
    ///     <see cref="Names" /> does; the client refers to a character by its position here. A character pending
    ///     deletion keeps its position but is not shown, so deleting one never moves another into its place.
    /// </summary>
    public static MobileEntity?[] Layout(IReadOnlyList<MobileEntity> characters, int maxPerAccount)
    {
        var layout = new MobileEntity?[maxPerAccount];
        var unplaced = new List<MobileEntity>();

        foreach (var character in characters)
        {
            if (character.Slot is { } slot && slot < maxPerAccount && layout[slot] is null)
            {
                layout[slot] = character;
            }
            else
            {
                unplaced.Add(character);
            }
        }

        foreach (var character in unplaced)
        {
            var free = Array.IndexOf(layout, null);

            if (free < 0)
            {
                break;
            }

            layout[free] = character;
        }

        for (var position = 0; position < layout.Length; position++)
        {
            if (layout[position]?.DeletionRequestedAt is not null)
            {
                layout[position] = null;
            }
        }

        return layout;
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
