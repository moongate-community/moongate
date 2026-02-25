using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.Characters;

/// <summary>
/// Persists runtime character position updates received from movement events.
/// </summary>
public interface ICharacterPositionPersistenceService : IMoongateService { }
