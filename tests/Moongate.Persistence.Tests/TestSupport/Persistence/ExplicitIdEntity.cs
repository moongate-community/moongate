using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class ExplicitIdEntity : IMoongateEntity
{
    Serial IMoongateEntity.Id => new(1);
}
