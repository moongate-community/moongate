using Moongate.Server.Ultima.Data.BulletinBoards;

namespace Moongate.Tests.Server.Ultima.Data.BulletinBoards;

public sealed class BulletinEquipmentTests
{
    [Fact]
    public void Format_ThenParse_GivesThePiecesBack()
    {
        BulletinEquipment[] worn = [new(0x1F03, 0x0021), new(0x203B, 0x044E)];

        var text = BulletinEquipment.Format(worn);

        Assert.Equal("7939:33,8251:1102", text);
        Assert.Equal(worn, BulletinEquipment.Parse(text));
    }

    [Fact]
    public void Parse_OfNothing_IsNothing()
    {
        Assert.Empty(BulletinEquipment.Parse(""));
        Assert.Equal("", BulletinEquipment.Format([]));
    }

    // A row somebody edited by hand: what cannot be read is left out, the rest is kept.
    [Fact]
    public void Parse_LeavesOutWhatIsNotAPair()
    {
        Assert.Equal([new BulletinEquipment(7939, 33)], BulletinEquipment.Parse("7939:33,horse,12,:,5:x"));
    }
}
