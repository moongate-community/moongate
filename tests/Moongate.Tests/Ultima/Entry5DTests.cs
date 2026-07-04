using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

public class Entry5DTests
{
    [Fact]
    public void Properties_RoundTripAssignedValues()
    {
        var entry = new Entry5D
        {
            File = 4,
            Index = 0x0ECA,
            Lookup = 1234,
            Length = 88,
            Extra = 7
        };

        Assert.Equal(4, entry.File);
        Assert.Equal(0x0ECA, entry.Index);
        Assert.Equal(1234, entry.Lookup);
        Assert.Equal(88, entry.Length);
        Assert.Equal(7, entry.Extra);
    }
}
