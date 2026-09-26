namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     The sounds of a mobile template. Each is inherited through the base template on its own, so a base such as
///     <c>
///         base_orc
///     </c>
///     sets them once for every orc; an unset sound is not played.
/// </summary>
public class MobileSounds
{
    /// <summary>
    ///     Played when the mobile starts a fight.
    /// </summary>
    public int? StartAttack { get; set; }

    /// <summary>
    ///     Played now and then while the mobile is idle.
    /// </summary>
    public int? Idle { get; set; }

    /// <summary>
    ///     Played when the mobile hits.
    /// </summary>
    public int? Attack { get; set; }

    /// <summary>
    ///     Played when the mobile is hit.
    /// </summary>
    public int? Hurt { get; set; }

    /// <summary>
    ///     Played when the mobile dies.
    /// </summary>
    public int? Death { get; set; }
}
