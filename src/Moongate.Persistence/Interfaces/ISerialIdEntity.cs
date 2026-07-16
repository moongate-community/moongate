using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces;

/// <summary>
/// An entity uniquely identified by a <see cref="Serial" />, allowing it to be tracked and
/// persisted by its serial id.
/// </summary>
public interface ISerialIdEntity
{
    /// <summary>The entity's unique serial identifier.</summary>
    Serial Id { get; set; }
}
