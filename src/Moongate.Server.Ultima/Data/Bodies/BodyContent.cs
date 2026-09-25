using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Bodies;

/// <summary>
///     One body of <c>bodies.toml</c> with the kind of creature it is.
/// </summary>
public class BodyContent
{
    public Body Body { get; set; }

    public BodyType Type { get; set; }
}
