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
        /// <returns>Resolved item id (`baseItemId + 2 * facing`).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the facing value is unknown.</exception>
        public int ToItemId(int baseItemId)
        {
            if (!Enum.IsDefined(facing))
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "Invalid door facing.");
            }

            return baseItemId + (2 * (int)facing);
        }
    }
}
