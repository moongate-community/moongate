namespace Moongate.Server.Ultima.Types.Mobiles;

/// <summary>
///     The gender a mobile template gives the mobiles made from it.
/// </summary>
public enum MobileGenderType : byte
{
    /// <summary>
    ///     Always male.
    /// </summary>
    Male = 0,

    /// <summary>
    ///     Always female.
    /// </summary>
    Female = 1,

    /// <summary>
    ///     Male or female, picked 50/50 for every mobile.
    /// </summary>
    Random = 2
}
