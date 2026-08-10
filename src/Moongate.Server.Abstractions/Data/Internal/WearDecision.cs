using Moongate.Ultima.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// Whether a held item may be worn, and where it goes. The layer travels with the answer because it is
/// derived from the item rather than taken from the request, so the caller has no other way to know it.
/// </summary>
/// <param name="Accepted">True when the item may be worn.</param>
/// <param name="Layer">The layer the item occupies, or <see cref="LayerType.None" /> when it wears nowhere.</param>
public readonly record struct WearDecision(bool Accepted, LayerType Layer);
