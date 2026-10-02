using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     A control whose answer goes into the argument <see cref="Name" />: a text entry by entry id, a checkbox or radio
///     by switch id.
/// </summary>
public sealed class GumpBind
{
    public required string Name { get; init; }

    public required GumpBindType Kind { get; init; }

    public required int Id { get; init; }
}
