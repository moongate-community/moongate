using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Changes how a mobile looks for a while, as the spells Incognito and Polymorph do: its body, skin hue or name are
///     replaced and given back when the time is up, when the mobile dies or when it comes back after a restart. What it
///     was is kept with the mobile as props, so a disguise never outlasts the mobile.
/// </summary>
public interface IDisguiseService
{
    /// <summary>
    ///     Disguises the mobile for <paramref name="duration" />; false, with nothing changed, for one that is dead, one
    ///     already disguised, a duration that is not positive, a name the mobile cannot have or nothing to change. A
    ///     rider that takes a body that is not a human one is dismounted.
    /// </summary>
    bool Disguise(MobileEntity mobile, DisguiseLooks looks, TimeSpan duration);

    /// <summary>
    ///     Gets whether the mobile is disguised.
    /// </summary>
    bool IsDisguised(MobileEntity mobile);

    /// <summary>
    ///     Gives the mobile its own body, hue and name back; false when it was not disguised.
    /// </summary>
    bool End(MobileEntity mobile);

    /// <summary>
    ///     Takes a saved disguise up again, as a disguised player comes back: ended when its time passed, else timed for
    ///     what is left.
    /// </summary>
    void Resume(MobileEntity mobile);
}
