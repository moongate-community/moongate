namespace Moongate.UO.Data.Factory;

public class ItemTemplate : BaseTemplate
{
    public string ItemId { get; set; }

    public string Hue { get; set; }

    public int GoldValue { get; set; } = 1;

    public double Weight { get; set; } = 1.0;

}
