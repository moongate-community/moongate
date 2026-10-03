using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Data.Movement;

/// <summary>
///     What an NPC walking to a place does now, and for <see cref="NpcWalkType.Moving" /> the direction of its step.
/// </summary>
public readonly record struct NpcPathStep(NpcWalkType Kind, DirectionType Direction = DirectionType.North);
