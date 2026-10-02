using System.Xml.Linq;

namespace Moongate.Server.Ultima.Data.Templates.Gumps;

/// <summary>
///     A gump of <c>templates/gumps</c>, checked against <c>gump.xsd</c>: its id, the file it came from and its
///     <c>&lt;gump&gt;</c> element.
/// </summary>
public sealed class GumpTemplate
{
    public required string Id { get; init; }

    public required string File { get; init; }

    public required XElement Root { get; init; }
}
