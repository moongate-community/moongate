using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published after a new player character and its starting items were saved.
/// </summary>
/// <param name="Character">
///     The saved character.
/// </param>
/// <param name="Items">
///     Its starting items, the backpack first.
/// </param>
public sealed record CharacterCreatedEvent(MobileEntity Character, IReadOnlyList<ItemEntity> Items) : IMoongateEvent;
