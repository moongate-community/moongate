namespace Moongate.Ultima.Types;

/// <summary>
/// UO equipment layer as used on the wire and in tiledata (the Quality byte of
/// wearable items). Zero means "not a wearable layer".
/// </summary>
public enum LayerType : byte
{
    None = 0,
    OneHanded = 1,
    TwoHanded = 2,
    Shoes = 3,
    Pants = 4,
    Shirt = 5,
    Helm = 6,
    Gloves = 7,
    Ring = 8,
    Talisman = 9,
    Neck = 10,
    Hair = 11,
    Waist = 12,
    InnerTorso = 13,
    Bracelet = 14,
    Face = 15,
    FacialHair = 16,
    MiddleTorso = 17,
    Earrings = 18,
    Arms = 19,
    Cloak = 20,
    Backpack = 21,
    OuterTorso = 22,
    OuterLegs = 23,
    InnerLegs = 24,
    Mount = 25,
    ShopBuy = 26,
    ShopResale = 27,
    ShopSell = 28,
    Bank = 29
}
