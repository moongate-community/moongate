using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class UpgradeV3Entity : IMoongateEntity
{
    public Serial Id { get; set; }

    public string DisplayName { get; set; } = "";

    public long Level { get; set; } = 1;
}
