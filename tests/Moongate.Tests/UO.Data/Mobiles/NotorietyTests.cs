using Moongate.UO.Data.Mobiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Mobiles;

public class NotorietyTests
{
    [Theory, InlineData(4, false), InlineData(5, true), InlineData(6, true)]
    public void IsMurderer_TripsAtFiveKills(int kills, bool expected)
        => Assert.Equal(expected, Notoriety.IsMurderer(kills));

    [Theory, InlineData(0, false, NotorietyType.Innocent), InlineData(4, false, NotorietyType.Innocent),
     InlineData(0, true, NotorietyType.Criminal), InlineData(5, false, NotorietyType.Murderer),
     InlineData(9, false, NotorietyType.Murderer), InlineData(5, true, NotorietyType.Murderer)]

    // one kill short of the threshold
    // the threshold itself
    // Murderer outranks criminal: a red flagged criminal still reads red.
    public void Resolve_FollowsMurdererThenCriminalThenInnocent(int kills, bool criminal, NotorietyType expected)
        => Assert.Equal(expected, Notoriety.Resolve(kills, criminal));
}
