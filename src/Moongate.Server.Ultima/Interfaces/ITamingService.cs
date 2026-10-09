using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Taming;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What can be tamed: the creatures of <c>data/taming.toml</c>, by the id of their mobile template.
/// </summary>
public interface ITamingService
{
    /// <summary>
    ///     Gets the number of creatures that can be tamed.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets the taming data of a mobile template; false for a creature that cannot be tamed.
    /// </summary>
    bool TryGet(string templateId, [NotNullWhen(true)] out TamingCreature? creature);
}
