using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     Who gets starting items: the new character, its body, its skills and the hues picked for its shirt and pants
///     (0 keeps the item's hue).
/// </summary>
public sealed record StartingItemsRequest(
    Serial MobileId,
    RaceType Race,
    GenderType Gender,
    IReadOnlyDictionary<SkillType, int> Skills,
    Hue ShirtHue,
    Hue PantsHue
);
