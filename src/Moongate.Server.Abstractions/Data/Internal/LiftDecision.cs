using Moongate.Network.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// The outcome of the lift rules: accepted, or refused with the reason the client is told via 0x27.
/// <see cref="Reason" /> is meaningless when <see cref="Accepted" /> is true.
/// </summary>
public readonly record struct LiftDecision(bool Accepted, LiftRejectReasonType Reason);
