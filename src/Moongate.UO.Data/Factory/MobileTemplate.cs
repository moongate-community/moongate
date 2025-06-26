namespace Moongate.UO.Data.Factory;

public class MobileTemplate : BaseTemplate
{
    public int Body { get; set; }

    public int SkinHue { get; set; }

    public int HairHue { get; set; }

    public int HairStyle { get; set; }

    public int Strength { get; set; } = 50;

    public int Dexterity { get; set; } = 50;

    public int Intelligence { get; set; } = 50;

    public int Hits { get; set; } = 100;

    public int Mana { get; set; } = 100;

    public int Stamina { get; set; } = 100;
}
