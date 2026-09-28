using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Characters;

/// <summary>
///     A character chosen to play and the items it wears, backpack included.
/// </summary>
public sealed record CharacterForPlay(MobileEntity Character, IReadOnlyList<ItemEntity> Equipment);
