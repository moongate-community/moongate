using Moongate.Core.Types;

namespace Moongate.Server.Abstractions.Data.Session;

/// <summary>A step that arrived early and is waiting for its turn, kept in the order it came.</summary>
/// <param name="Direction">The direction the client asked to face or step in.</param>
/// <param name="Sequence">The movement sequence number the client stamped it with.</param>
public readonly record struct QueuedMove(DirectionType Direction, byte Sequence);
