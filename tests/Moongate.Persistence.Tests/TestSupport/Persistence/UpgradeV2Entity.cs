using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class UpgradeV2Entity : IMoongateEntity
{
    public Serial Id { get; set; }

    public string DisplayName { get; set; } = "";

    public int Level { get; set; } = 1;
}
