using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class UpgradeV1Entity : IMoongateEntity
{
    public Serial Id { get; set; }

    public string Name { get; set; } = "";
}
