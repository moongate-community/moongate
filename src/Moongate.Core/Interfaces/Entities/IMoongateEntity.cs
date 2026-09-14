using Moongate.Core.Primitives;

namespace Moongate.Core.Interfaces.Entities;

/// <summary>Identifies an entity that can be stored by Moongate persistence.</summary>
public interface IMoongateEntity
{
    /// <summary>Gets the stable serial that identifies the entity within its collection.</summary>
    Serial Id { get; }
}
