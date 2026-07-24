using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a new character is fully provisioned — backpack, bank box and starting items equipped
/// and persisted — and is ready to enter the world. <see cref="BackpackId" /> and <see cref="BankId" />
/// are <see cref="Serial.Zero" /> when the corresponding container template was missing.
/// </summary>
public sealed record CharacterReadyEvent(
    Serial AccountId,
    MobileEntity Character,
    Serial BackpackId,
    Serial BankId
) : ILoopAffineEvent;
