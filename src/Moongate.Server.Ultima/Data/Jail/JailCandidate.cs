using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Data.Jail;

/// <summary>
///     A player character the jail found by its name, in the world or not: who it is and the rank of its account.
/// </summary>
public sealed class JailCandidate
{
    public Serial Id { get; init; }

    public string Name { get; init; } = "";

    /// <summary>
    ///     The name of the account the character belongs to: what tells two characters of one name apart.
    /// </summary>
    public string Account { get; init; } = "";

    public AccountType AccountType { get; init; }
}
