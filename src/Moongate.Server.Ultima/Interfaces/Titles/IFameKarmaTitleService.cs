using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Interfaces.Titles;

/// <summary>
///     Resolves a reputation title prefix from a mobile's current fame, karma, and gender.
/// </summary>
public interface IFameKarmaTitleService
{
    /// <summary>
    ///     Returns the matching prefix, or an empty string for an untitled band.
    /// </summary>
    string GetTitle(int fame, int karma, GenderType gender);

    /// <summary>
    ///     Resolves the current scores of a mobile without changing its custom title.
    /// </summary>
    string GetTitle(MobileEntity mobile);
}
