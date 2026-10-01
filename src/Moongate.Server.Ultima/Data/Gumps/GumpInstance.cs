using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A gump to open for one player: its id (one open gump per id and player), its layout, where it opens, and what runs
///     when the player answers or closes it.
/// </summary>
public sealed class GumpInstance
{
    public required string Id { get; init; }

    public required GumpLayout Layout { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>
    ///     Gets what runs on the game loop with the player's answer; button 0 means the gump was closed.
    /// </summary>
    public required Action<GameSession, GumpResponse> OnResponse { get; init; }
}
