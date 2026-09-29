using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     A character chosen to play: the items it wears, backpack included, and everything inside them at any depth.
/// </summary>
public sealed record CharacterForPlay(
    MobileEntity Character,
    IReadOnlyList<ItemEntity> Equipment,
    IReadOnlyList<ItemEntity> Contents
);
