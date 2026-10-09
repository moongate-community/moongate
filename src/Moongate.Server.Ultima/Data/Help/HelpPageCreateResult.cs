using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Help;

namespace Moongate.Server.Ultima.Data.Help;

/// <summary>
///     The answer of the queue to a request for a game master.
/// </summary>
public sealed class HelpPageCreateResult
{
    /// <summary>
    ///     Gets what happened.
    /// </summary>
    public HelpPageCreateResultType Type { get; init; }

    /// <summary>
    ///     Gets the request that was made; null unless <see cref="Type" /> is Ok.
    /// </summary>
    public HelpPageEntity? Page { get; init; }

    /// <summary>
    ///     Gets the seconds still to wait when <see cref="Type" /> is Wait.
    /// </summary>
    public int WaitSeconds { get; init; }
}
