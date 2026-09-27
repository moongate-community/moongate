using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Templates.Mobiles;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Everything <see cref="MobileTemplateBuilder" /> resolves a block against.
/// </summary>
/// <param name="Dictionary">UOX3 dictionary texts by id, for numeric names and titles.</param>
/// <param name="Items">The item pass's ids.</param>
/// <param name="MobileHeaders">Every converted npc header, for <c>GET</c> targets.</param>
/// <param name="ColorLists">Each <c>[RANDOMCOLOR n]</c> as a hue range, or null when it is not one run.</param>
/// <param name="CreatureSounds">The sounds of each body, from <c>creatures.dfn</c>.</param>
/// <param name="Report">Where dropped values are counted.</param>
internal sealed record MobileBuildContext(
    IReadOnlyDictionary<int, string> Dictionary,
    ItemIndex Items,
    IReadOnlySet<string> MobileHeaders,
    IReadOnlyDictionary<int, HueSpec?> ColorLists,
    IReadOnlyDictionary<int, MobileSounds> CreatureSounds,
    ConversionReport Report
);
