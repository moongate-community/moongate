using Moongate.Server.Ultima.Data.Gumps;

namespace Moongate.Server.Ultima.Data.Internal.Gumps;

/// <summary>
///     A gump sent to a player and not answered yet: its serial and type id, and what it offered.
/// </summary>
public sealed class OpenGump
{
    public required uint Serial { get; init; }

    public required uint TypeId { get; init; }

    public required GumpInstance Gump { get; init; }

    public required GumpBuildResult Built { get; init; }
}
