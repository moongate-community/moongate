using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class InventoryEntity : IMoongateEntity
{
    public Serial Id { get; set; }

    public int Balance { get; set; }
}
