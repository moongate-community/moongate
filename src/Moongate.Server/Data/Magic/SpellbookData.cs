namespace Moongate.Server.Data.Magic;

/// <summary>
/// Immutable bitfield wrapper for spell entries stored on a spellbook item.
/// </summary>
public readonly struct SpellbookData
{
    public ulong Content { get; }

    public SpellbookData(ulong content)
    {
        Content = content;
    }

    public bool HasSpell(int spellId)
        => (Content & GetBit(spellId)) != 0;

    public SpellbookData WithSpell(int spellId)
        => new(Content | GetBit(spellId));

    public SpellbookData WithoutSpell(int spellId)
        => new(Content & ~GetBit(spellId));

    private static ulong GetBit(int spellId)
    {
        if (spellId is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(spellId), "Spell id must be between 1 and 64.");
        }

        return 1UL << (spellId - 1);
    }
}
