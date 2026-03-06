using Moongate.Server.Types.World;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.World;

/// <summary>
/// Provides conversion helpers for door generation facing values.
/// </summary>
public static class DoorGenerationFacingExtensions
{
    /// <param name="facing">Door generation facing value.</param>
    extension(DoorGenerationFacing facing)
    {
        /// <summary>
        /// Converts a door generation facing to the equivalent world direction.
        /// </summary>
        /// <returns>Mapped <see cref="DirectionType"/> value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the facing value is unknown.</exception>
        public DirectionType ToDirectionType()
            => facing switch
            {
                DoorGenerationFacing.WestCW => DirectionType.West,
                DoorGenerationFacing.EastCCW => DirectionType.East,
                DoorGenerationFacing.SouthCW => DirectionType.South,
                DoorGenerationFacing.NorthCCW => DirectionType.North,
                _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, "Invalid door facing.")
            };

        /// <summary>
        /// Computes the concrete door item id from a base door item id and the facing.
        /// </summary>
        /// <param name="baseItemId">Base item id for the door family.</param>
        /// <returns>Resolved closed door item id for the requested facing.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the facing value is unknown.</exception>
        public int ToItemId(int baseItemId)
        {
            if (!Enum.IsDefined(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "Invalid door facing.");
            }

            // ModernUO closed-door piece order:
            // WestCW(base), EastCCW(base+2), SouthCW(base+8), NorthCCW(base+10).
            return facing switch
            {
                DoorGenerationFacing.WestCW => baseItemId,
                DoorGenerationFacing.EastCCW => baseItemId + 2,
                DoorGenerationFacing.SouthCW => baseItemId + 8,
                DoorGenerationFacing.NorthCCW => baseItemId + 10,
                _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, "Invalid door facing.")
            };
        }
    }
}
