using Moongate.Server.Ultima.Types.Templates;

namespace Moongate.Server.Ultima.Data.Internal.Tooltips;

/// <summary>
///     Everything of an item its tooltip depends on; the rest (templates, tiledata, messages) does not change while the
///     server runs, so equal keys give equal tooltips.
/// </summary>
internal readonly record struct ItemTooltipKey(
    string TemplateId,
    int ItemId,
    int Amount,
    string? Name,
    ItemRarityType Rarity,
    LootType? LootType,
    bool? Movable
);
