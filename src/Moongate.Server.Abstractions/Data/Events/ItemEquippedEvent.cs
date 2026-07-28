using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Ultima.Types;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>Raised when an item is placed on a mobile's paperdoll layer.</summary>
public sealed record ItemEquippedEvent(Serial Item, Serial Mobile, LayerType Layer) : ILoopAffineEvent;
