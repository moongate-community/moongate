using Moongate.UO.Data.Items;

namespace Moongate.Tests.Data.Items;

/// <summary>
/// An item's name cliloc is derived from its graphic, which is how UO names anything nobody
/// renamed. The two bases meet at 0x4000.
/// </summary>
public class ItemClilocsTests
{
    [Theory, InlineData(3821, 1023821), InlineData(5137, 1025137), InlineData(0, 1020000), InlineData(0x3FFF, 1036383),
     InlineData(0x4000, 1095256), InlineData(9100, 1029100)]

    // gold coin
    // platemail legs
    // last id on the classic base
    // first id on the extended base
     // book of bushido
    public void ForItemId_MapsThroughTheRightBase(int itemId, int expected)
        => Assert.Equal(expected, ItemClilocs.ForItemId(itemId));
}
