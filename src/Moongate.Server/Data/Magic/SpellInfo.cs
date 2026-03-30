using Moongate.Server.Types.Magic;

namespace Moongate.Server.Data.Magic;

public sealed record SpellInfo
{
    public SpellInfo(string name, string mantra, ReagentType[] reagents, int[] reagentAmounts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(mantra);
        ArgumentNullException.ThrowIfNull(reagents);
        ArgumentNullException.ThrowIfNull(reagentAmounts);

        if (reagents.Length != reagentAmounts.Length)
        {
            throw new ArgumentException("Reagent arrays must have the same length.", nameof(reagentAmounts));
        }

        Name = name;
        Mantra = mantra;
        Reagents = [.. reagents];
        ReagentAmounts = [.. reagentAmounts];
    }

    public string Name { get; init; }

    public string Mantra { get; init; }

    public ReagentType[] Reagents { get; init; }

    public int[] ReagentAmounts { get; init; }
}
