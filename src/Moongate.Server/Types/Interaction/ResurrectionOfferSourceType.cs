global using Moongate.Server.Types.Interaction;

namespace Moongate.Server.Types.Interaction;

/// <summary>
/// Identifies the runtime source that offered resurrection.
/// </summary>
public enum ResurrectionOfferSourceType : byte
{
    /// <summary>
    /// Resurrection offered by a healer mobile.
    /// </summary>
    Healer = 1,

    /// <summary>
    /// Resurrection offered by an ankh or shrine item.
    /// </summary>
    Ankh = 2
}
