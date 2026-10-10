namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     What a circle of Magery asks, by the classic tables: the mana, the cast delay and the skill window of a try.
/// </summary>
internal static class SpellCircleRules
{
    /// <summary>
    ///     The delay that must pass after a cast ends before another can begin.
    /// </summary>
    public const double RecoverySeconds = 0.75;

    /// <summary>
    ///     The least a recovery after a disturbed cast lasts.
    /// </summary>
    public const double MinimumDisturbRecoverySeconds = 0.2;

    /// <summary>
    ///     How many circles of the skill window a scroll is easier than a book.
    /// </summary>
    private const int ScrollCircles = 2;

    private const double WindowLength = 40;
    private const double CastDelayBase = 0.5;
    private const double CastDelayPerCircle = 0.25;

    private static readonly int[] ManaByCircle = [4, 6, 9, 11, 14, 20, 40, 50];

    // The first two are the circles a scroll is easier than the first one; the window of a circle is its entry from the
    // third, a scroll's two before.
    private static readonly double[] RequiredSkill = [-50.0, -30.0, 0.0, 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0];

    /// <summary>
    ///     Gets the mana a circle (1 to 8) costs.
    /// </summary>
    public static int Mana(int circle)
    {
        return ManaByCircle[Math.Clamp(circle, 1, ManaByCircle.Length) - 1];
    }

    /// <summary>
    ///     Gets the seconds the cast of a circle (1 to 8) takes before the target.
    /// </summary>
    public static double CastDelay(int circle)
    {
        return CastDelayBase + CastDelayPerCircle * (Math.Clamp(circle, 1, ManaByCircle.Length) - 1);
    }

    /// <summary>
    ///     Gets the Magery points a try of a circle needs to just begin to succeed, and where it never fails; a scroll is
    ///     two circles easier than a book.
    /// </summary>
    public static (double Min, double Max) SkillWindow(int circle, bool scroll)
    {
        var index = Math.Clamp(circle, 1, ManaByCircle.Length) - 1 + (scroll ? 0 : ScrollCircles);
        var min = RequiredSkill[index];

        return (min, min + WindowLength);
    }

    /// <summary>
    ///     Gets the seconds before another cast after one was disturbed <paramref name="elapsed" /> seconds into a delay of
    ///     <paramref name="delay" />: the less of the cast was done, the longer.
    /// </summary>
    public static double DisturbRecovery(double elapsed, double delay)
    {
        var done = delay > 0 ? Math.Sqrt(Math.Max(elapsed, 0) / delay) : 1;

        return Math.Max(1.0 - done, MinimumDisturbRecoverySeconds);
    }
}
