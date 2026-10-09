using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Help;

namespace Moongate.Tests.Server.Ultima.Entities.World;

public sealed class HelpPageEntityTests
{
    [Theory,
     InlineData(HelpPageStatusType.Open, true),
     InlineData(HelpPageStatusType.Taken, true),
     InlineData(HelpPageStatusType.Closed, false)]
    public void IsActive_IsTrueUntilTheyAreClosed(HelpPageStatusType status, bool active)
    {
        Assert.Equal(active, new HelpPageEntity { Status = status }.IsActive);
    }

    [Fact]
    public void Snapshot_IsADetachedCopy()
    {
        var page = new HelpPageEntity { Id = new Serial(7), Text = "Stuck", Status = HelpPageStatusType.Open };

        var copy = page.Snapshot();
        copy.Status = HelpPageStatusType.Closed;

        Assert.NotSame(page, copy);
        Assert.Equal(HelpPageStatusType.Open, page.Status);
        Assert.Equal((new Serial(7), "Stuck", HelpPageStatusType.Closed), (copy.Id, copy.Text, copy.Status));
    }
}
