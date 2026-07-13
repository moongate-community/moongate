using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;

namespace Moongate.Server.Interfaces;

public interface ICharacterService
{
    IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId);

}
