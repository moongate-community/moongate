using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Interfaces;

namespace Moongate.Tests.Support;

/// <summary>Test double for <see cref="ICharacterService"/>: returns no characters for any account.</summary>
public sealed class StubCharacterService : ICharacterService
{
    public IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId)
    {
        return [];
    }
}
