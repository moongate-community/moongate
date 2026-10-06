namespace Moongate.Server.Ultima.Types.Effects;

/// <summary>
///     The animated graphics the emulators use for effects, with the art ids ModernUO passes, named after the client
///     tile data and after what the other emulators use them for. Any other art id works as an effect graphic too.
/// </summary>
public enum EffectGraphicType
{
    /// <summary>
    ///     No graphic: an effect made only of particles.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Water splash.
    /// </summary>
    Splash = 0x352D,

    /// <summary>
    ///     Explosion; explosion potions and the Explosion spell's blast.
    /// </summary>
    Explosion = 0x36B0,

    /// <summary>
    ///     Explosion, second animation; the Explosion spell on its target.
    /// </summary>
    ExplosionBurst = 0x36BD,

    /// <summary>
    ///     Explosion, third animation.
    /// </summary>
    ExplosionFlash = 0x36CB,

    /// <summary>
    ///     Large fireball; the Fireball spell and fire breath.
    /// </summary>
    LargeFireball = 0x36D4,

    /// <summary>
    ///     Small fireball; the Magic Arrow spell.
    /// </summary>
    SmallFireball = 0x36E4,

    /// <summary>
    ///     Fire snake; the Fire Bolt.
    /// </summary>
    FireSnake = 0x36F4,

    /// <summary>
    ///     Explosion ball.
    /// </summary>
    ExplosionBall = 0x36FE,

    /// <summary>
    ///     Column of fire; the Flamestrike spell.
    /// </summary>
    FireColumn = 0x3709,

    /// <summary>
    ///     Puff of smoke; teleports, gates and anything that appears or vanishes.
    /// </summary>
    Smoke = 0x3728,

    /// <summary>
    ///     Fizzle; a spell that fails.
    /// </summary>
    Fizzle = 0x3735,

    /// <summary>
    ///     Sparkle used for blessings.
    /// </summary>
    SparkleBless = 0x373A,

    /// <summary>
    ///     Sparkle used for curses.
    /// </summary>
    SparkleCurse = 0x374A,

    /// <summary>
    ///     Sparkle used for protections and other spells on oneself.
    /// </summary>
    Sparkle = 0x375A,

    /// <summary>
    ///     Sparkle used for healing; the most used effect of all.
    /// </summary>
    SparkleHeal = 0x376A,

    /// <summary>
    ///     Sparkle, fifth animation.
    /// </summary>
    SparkleSwirl = 0x3779,

    /// <summary>
    ///     Death vortex.
    /// </summary>
    DeathVortex = 0x3789,

    /// <summary>
    ///     Glowing arrow.
    /// </summary>
    GlowingArrow = 0x379E,

    /// <summary>
    ///     Small bolt; the Energy Bolt spell.
    /// </summary>
    SmallBolt = 0x379F,

    /// <summary>
    ///     Field of blades.
    /// </summary>
    FieldOfBlades = 0x37A0,

    /// <summary>
    ///     Glow.
    /// </summary>
    Glow = 0x37B9,

    /// <summary>
    ///     Glow, second animation.
    /// </summary>
    GlowPulse = 0x37BE,

    /// <summary>
    ///     Glow, third animation.
    /// </summary>
    GlowBurst = 0x37C4,

    /// <summary>
    ///     Death vortex, second animation.
    /// </summary>
    DeathVortexLarge = 0x37CC,

    /// <summary>
    ///     Energy.
    /// </summary>
    Energy = 0x3818,

    /// <summary>
    ///     Field of poison running east to west.
    /// </summary>
    PoisonFieldEastWest = 0x3915,

    /// <summary>
    ///     Field of poison running north to south.
    /// </summary>
    PoisonFieldNorthSouth = 0x3922,

    /// <summary>
    ///     Field of energy running east to west.
    /// </summary>
    EnergyFieldEastWest = 0x3946,

    /// <summary>
    ///     Field of energy running north to south.
    /// </summary>
    EnergyFieldNorthSouth = 0x3956,

    /// <summary>
    ///     Field of paralysis running east to west.
    /// </summary>
    ParalysisFieldEastWest = 0x3967,

    /// <summary>
    ///     Field of paralysis running north to south.
    /// </summary>
    ParalysisFieldNorthSouth = 0x3979,

    /// <summary>
    ///     Field of fire running east to west.
    /// </summary>
    FireFieldEastWest = 0x398C,

    /// <summary>
    ///     Field of fire running north to south.
    /// </summary>
    FireFieldNorthSouth = 0x3996
}
