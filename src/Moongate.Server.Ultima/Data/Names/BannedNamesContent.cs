namespace Moongate.Server.Ultima.Data.Names;

/// <summary>
///     The words of <c>banned_names.toml</c> that a player character name may not use. Matching ignores case.
/// </summary>
public class BannedNamesContent
{
    /// <summary>
    ///     Words a name may not start with; <c>gm</c> also bans <c>GMaria</c>.
    /// </summary>
    public List<string> StartsWith { get; set; } = [];

    /// <summary>
    ///     Words a name may not contain as a whole word; <c>mage</c> bans <c>Aria the Mage</c> but not <c>Magenta</c>.
    /// </summary>
    public List<string> Words { get; set; } = [];

    /// <summary>
    ///     Returns whether <paramref name="name" /> starts with a banned word or contains one as a whole word, split on
    ///     <paramref name="separators" />.
    /// </summary>
    public bool IsBanned(string name, char[] separators)
    {
        if (StartsWith.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return name.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(word => Words.Contains(word, StringComparer.OrdinalIgnoreCase));
    }
}
