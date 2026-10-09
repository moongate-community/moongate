namespace Moongate.Server.Ultima.Data.Taming;

/// <summary>
///     The root of <c>data/taming.toml</c>: one <c>[[creature]]</c> per creature that can be tamed.
/// </summary>
public class TamingFile
{
    public List<TamingCreature> Creature { get; set; } = [];
}
