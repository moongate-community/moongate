using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Death;

namespace Moongate.Server.Ultima.Data.Death;

/// <summary>
///     The outcome of raising a corpse: who is back, or why nobody is.
/// </summary>
public sealed record ResurrectResult(ResurrectResultType Type, MobileEntity? Mobile);
