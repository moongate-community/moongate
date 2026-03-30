using Moongate.Server.Types.Magic;

namespace Moongate.Server.Services.Magic.Base;

/// <summary>
/// Base class for regular magery spells with circle-driven mana and cast delay rules.
/// </summary>
public abstract class MagerySpellBase : SpellBase
{
    private static readonly int[] ManaCostTable = [4, 6, 9, 11, 14, 20, 40, 50];
    private static readonly double[] CastDelaySecondsTable = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.5, 2.75];

    public override SpellbookType SpellbookType => SpellbookType.Regular;

    public abstract SpellCircleType Circle { get; }

    public override int ManaCost => ManaCostTable[ResolveCircleIndex()];

    public override TimeSpan CastDelay => TimeSpan.FromSeconds(CastDelaySecondsTable[ResolveCircleIndex()]);

    private int ResolveCircleIndex()
    {
        if (Circle < SpellCircleType.First || Circle > SpellCircleType.Eighth)
        {
            throw new InvalidOperationException($"Spell circle '{Circle}' is not valid for magery.");
        }

        return (int)Circle - 1;
    }
}
